using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Nice.HttpProxy.Utilities
{
    internal static class HttpTransformer
    {
        public static ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest)
        {
            foreach (var header in httpContext.Request.Headers)
            {
                var headerName = header.Key;
                var headerValue = header.Value;
                if (RequestUtilities.ShouldSkipRequestHeader(headerName))
                {
                    continue;
                }

                RequestUtilities.AddHeader(proxyRequest, headerName, headerValue);
            }

            // https://datatracker.ietf.org/doc/html/rfc7230#section-3.3.3
            // If a message is received with both a Transfer-Encoding and a
            // Content-Length header field, the Transfer-Encoding overrides the
            // Content-Length.  Such a message might indicate an attempt to
            // perform request smuggling (Section 9.5) or response splitting
            // (Section 9.4) and ought to be handled as an error.  A sender MUST
            // remove the received Content-Length field prior to forwarding such
            // a message downstream.
            if (httpContext.Request.Headers.ContainsKey(HeaderNames.TransferEncoding)
                && httpContext.Request.Headers.ContainsKey(HeaderNames.ContentLength))
            {
                proxyRequest.Content?.Headers.Remove(HeaderNames.ContentLength);
            }


            return default;
        }

        public static ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage? proxyResponse)
        {
            if (proxyResponse is null)
            {
                return new ValueTask<bool>(false);
            }

            var responseHeaders = httpContext.Response.Headers;
            CopyResponseHeaders(proxyResponse.Headers, responseHeaders);
            if (proxyResponse.Content is not null)
            {
                CopyResponseHeaders(proxyResponse.Content.Headers, responseHeaders);
            }

            // https://datatracker.ietf.org/doc/html/rfc7230#section-3.3.3
            // If a message is received with both a Transfer-Encoding and a
            // Content-Length header field, the Transfer-Encoding overrides the
            // Content-Length.  Such a message might indicate an attempt to
            // perform request smuggling (Section 9.5) or response splitting
            // (Section 9.4) and ought to be handled as an error.  A sender MUST
            // remove the received Content-Length field prior to forwarding such
            // a message downstream.
            if (proxyResponse.Content is not null
                && proxyResponse.Headers.NonValidated.Contains(HeaderNames.TransferEncoding)
                && proxyResponse.Content.Headers.NonValidated.Contains(HeaderNames.ContentLength))
            {
                httpContext.Response.Headers.Remove(HeaderNames.ContentLength);
            }

            // For responses with status codes that shouldn't include a body,
            // we remove the 'Content-Length: 0' header if one is present.
            if (proxyResponse.Content is not null
                && IsBodylessStatusCode(proxyResponse.StatusCode)
                && proxyResponse.Content.Headers.NonValidated.TryGetValues(HeaderNames.ContentLength, out var contentLengthValue)
                && contentLengthValue.ToString() == "0")
            {
                httpContext.Response.Headers.Remove(HeaderNames.ContentLength);
            }

            return new ValueTask<bool>(true);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBodylessStatusCode(HttpStatusCode statusCode) =>
            statusCode switch
            {
                // A 1xx response is terminated by the end of the header section; it cannot contain content
                // or trailers.
                // See https://www.rfc-editor.org/rfc/rfc9110.html#section-15.2-2
                >= HttpStatusCode.Continue and < HttpStatusCode.OK => true,
                // A 204 response is terminated by the end of the header section; it cannot contain content
                // or trailers.
                // See https://www.rfc-editor.org/rfc/rfc9110.html#section-15.3.5-5
                HttpStatusCode.NoContent => true,
                // Since the 205 status code implies that no additional content will be provided, a server
                // MUST NOT generate content in a 205 response.
                // See https://www.rfc-editor.org/rfc/rfc9110.html#section-15.3.6-3
                HttpStatusCode.ResetContent => true,
                _ => false
            };
        private static void CopyResponseHeaders(HttpHeaders source, IHeaderDictionary destination)
        {
            // We want to append to any prior values, if any.
            // Not using Append here because it skips empty headers.
            foreach (var header in source.NonValidated)
            {
                var headerName = header.Key;
                if (RequestUtilities.ShouldSkipResponseHeader(headerName))
                {
                    continue;
                }

                destination[headerName] = RequestUtilities.Concat(destination[headerName], header.Value);
            }
        }
    }
}
