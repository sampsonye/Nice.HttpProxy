using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nice.HttpProxy.Utilities
{
    internal static class StreamCopier
    {
        private const int DefaultBufferSize = 65536;
        internal const long UnknownLength = -1;

        internal static async ValueTask CopyAsync(Stream input, Stream output, long promisedContentLength, CancellationToken cancellation)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            var read = 0;
            long contentLength = 0;
            try
            {
                while (true)
                {
                    read = 0;

                    // Issue a zero-byte read to the input stream to defer buffer allocation until data is available.
                    // Note that if the underlying stream does not supporting blocking on zero byte reads, then this will
                    // complete immediately and won't save any memory, but will still function correctly.
                    var zeroByteReadTask = input.ReadAsync(Memory<byte>.Empty, cancellation);
                    if (zeroByteReadTask.IsCompletedSuccessfully)
                    {
                        // Consume the ValueTask's result in case it is backed by an IValueTaskSource
                        _ = zeroByteReadTask.Result;
                    }
                    else
                    {
                        // Take care not to return the same buffer to the pool twice in case zeroByteReadTask throws
                        var bufferToReturn = buffer;
                        buffer = null;
                        ArrayPool<byte>.Shared.Return(bufferToReturn);

                        await zeroByteReadTask;

                        buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                    }

                    read = await input.ReadAsync(buffer.AsMemory(), cancellation);
                    contentLength += read;
                    // Normally this is enforced by the server, but it could get out of sync if something in the proxy modified the body.
                    if (promisedContentLength != UnknownLength && contentLength > promisedContentLength)
                    {
                        throw new InvalidOperationException("More bytes received than the specified Content-Length.");
                    }

                    // End of the source stream.
                    if (read == 0)
                    {
                        if (promisedContentLength == UnknownLength || contentLength == promisedContentLength)
                        {
                            return;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Sent {contentLength} request content bytes, but Content-Length promised {promisedContentLength}.");
                        }
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellation);

                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                if (buffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
    }
}
