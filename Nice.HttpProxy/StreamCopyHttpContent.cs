using Microsoft.AspNetCore.Http;
using Nice.HttpProxy.Utilities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nice.HttpProxy
{
    internal class StreamCopyHttpContent : HttpContent
    {
        private readonly MemoryStream _memoryStream;
        private readonly CancellationToken _cancellationToken;

        public StreamCopyHttpContent(MemoryStream body,CancellationToken cancellationToken)
        {
            _memoryStream = body;
            _cancellationToken = cancellationToken;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return SerializeToStreamAsync(stream, context,_cancellationToken);
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await stream.FlushAsync(_cancellationToken);
            _memoryStream.Seek(0, SeekOrigin.Begin);
            await StreamCopier.CopyAsync(_memoryStream, stream, Headers.ContentLength ?? StreamCopier.UnknownLength, _cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_memoryStream != null) { _memoryStream.Dispose(); }
        }

    }
}
