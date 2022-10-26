using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Nice.HttpProxy.Abstractions;
using Nice.HttpProxy.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nice.HttpProxy
{
    public class DefaultHttpForwarder : IHttpForwarder,IDisposable
    {
        private readonly Channel<ShadowRequestStruct> _channel;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private IOptionsMonitor<HttpForwarderOptions> _forwarderOptionsMonitor;
        public DefaultHttpForwarder(IOptionsMonitor<HttpForwarderOptions> forwarderOptionsMonitor)
        {
            _forwarderOptionsMonitor = forwarderOptionsMonitor;
            _channel = Channel.CreateUnbounded<ShadowRequestStruct>();
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(ChannelConsumeLoop, TaskCreationOptions.LongRunning);
        }

        public ValueTask SendAsync(HttpContext context, Func<HttpContext, IEnumerable<string>> destinations, Func<Uri, HttpRequestMessage, ValueTask>? requestTransform = null)
        {
            var destinctionRequestList = destinations.Invoke(context);
            return SendAsync(context, destinctionRequestList, requestTransform);
        }
       
        public async ValueTask SendAsync(HttpContext context, IEnumerable<string> destinations, Func<Uri, HttpRequestMessage, ValueTask>? requestTransform = null)
        {
            if (destinations == null || !destinations.Any())
            {
                throw new ArgumentException("Invalid destination.", nameof(destinations));
            }
            var allTargets = destinations.Select(d => RequestUtilities.MakeDestinationAddress(d, context.Request.Path, context.Request.QueryString)).ToList();

            var defaultTarget = allTargets.First();
            var httpMessage = await CreateRequestMessageAsync(context);
            try
            {
                var source = await SendCoreAsync(httpMessage, defaultTarget, requestTransform, context.RequestAborted);
                context.Response.StatusCode = (int)source.StatusCode;
                await HttpTransformer.TransformResponseAsync(context, source);
                await CopyResponseBodyAsync(source.Content, context.Response.Body, context.RequestAborted);
                if (allTargets.Count>1)
                {
                    var shadows = new ShadowRequestStruct
                    {
                        HttpRequestMessage = httpMessage,
                        FullUrls = allTargets.Skip(1).Select(a => new UrlRetryTimes { FullUrl = a, Retries = 0 }).ToList(),
                        RequestTransform = requestTransform
                    };
                    await _channel.Writer.WriteAsync(shadows, context.RequestAborted);
                }
                else
                {
                    httpMessage.Content?.Dispose();
                    httpMessage.Dispose();
                }
            }
            catch (Exception e)
            {
                await HandleRequestFailureAsync(context, e);
            }

        }

        private async ValueTask<HttpRequestMessage> CreateRequestMessageAsync(HttpContext context)
        {
            var destinationRequest = new HttpRequestMessage();
            destinationRequest.Method = RequestUtilities.GetHttpMethod(context.Request.Method);
            destinationRequest.Version = HttpVersion.Version11;
            destinationRequest.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

            if (context.Request.ContentLength > 0 || RequestUtilities.HasBody(context.Request.Method))
            {
                context.Request.Body.Seek(0, SeekOrigin.Begin);
                var ms = new MemoryStream();
                await context.Request.Body.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);
                destinationRequest.Content = new StreamCopyHttpContent(ms, context.RequestAborted);
            }

            await HttpTransformer.TransformRequestAsync(context, destinationRequest);

            return destinationRequest;
        }

        private async ValueTask CopyResponseBodyAsync(HttpContent destinationResponseContent, Stream clientResponseStream, CancellationToken cancellation)
        {
            if (destinationResponseContent is not null)
            {
                using var destinationResponseStream = await destinationResponseContent.ReadAsStreamAsync(cancellation);
                // The response content-length is enforced by the server.
                await StreamCopier.CopyAsync(destinationResponseStream, clientResponseStream, StreamCopier.UnknownLength, cancellation);
            }
        }
        
        private async ValueTask<HttpResponseMessage> SendCoreAsync(HttpRequestMessage message, Uri target, Func<Uri, HttpRequestMessage, ValueTask>? requestTransform = null, CancellationToken cancellationToken = default)
        {
            var client = HttpMessageInvokerFactory.CreateClient();
            message.RequestUri = target;
            if (requestTransform != null)
            {
                await requestTransform.Invoke(target, message);
            }
            return await client.SendAsync(message, cancellationToken);
        }


        private async ValueTask HandleRequestFailureAsync(HttpContext context, Exception requestException)
        {
            if (requestException is OperationCanceledException)
            {
                if (context.RequestAborted.IsCancellationRequested)
                {
                    await ReportErrorAsync(StatusCodes.Status502BadGateway);
                }
                else
                {
                    await ReportErrorAsync(StatusCodes.Status504GatewayTimeout);
                }
            }

            // We couldn't communicate with the destination.
            await ReportErrorAsync(StatusCodes.Status502BadGateway);

            ValueTask ReportErrorAsync(int statusCode)
            {
                context.Response.StatusCode = statusCode;

                return ValueTask.CompletedTask;
            }
        }


        private async ValueTask ChannelConsumeLoop()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && await _channel.Reader.WaitToReadAsync())
            {
                var shadow = await _channel.Reader.ReadAsync(_cancellationTokenSource.Token);
                var passUrls = new List<UrlRetryTimes>(shadow.FullUrls.Count);
                foreach (var item in shadow.FullUrls)
                {
                    try
                    {
                        await SendCoreAsync(shadow.HttpRequestMessage, item.FullUrl, shadow.RequestTransform, _cancellationTokenSource.Token);
                        passUrls.Add(item);
                    }
                    catch (Exception ex)
                    {
                        var handler = _forwarderOptionsMonitor.CurrentValue.ShadowSendExceptionHandler;
                        if (handler != null)
                            await _forwarderOptionsMonitor.CurrentValue.ShadowSendExceptionHandler.Invoke(item.FullUrl,item.Retries,shadow.HttpRequestMessage,ex);
                        item.Retries++;
                        if (item.Retries >= _forwarderOptionsMonitor.CurrentValue.MaxRetries)
                        {
                            passUrls.Add(item);//让它过掉
                        }
                    }
                }
                shadow.FullUrls.RemoveAll(s => passUrls.Contains(s));
                if (shadow.FullUrls.Any())
                {
                    await _channel.Writer.WriteAsync(shadow, _cancellationTokenSource.Token);
                }
                else
                {
                    shadow.HttpRequestMessage.Content?.Dispose();
                    shadow.HttpRequestMessage.Dispose();
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
