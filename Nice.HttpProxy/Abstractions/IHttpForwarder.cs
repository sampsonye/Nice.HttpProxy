using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nice.HttpProxy.Abstractions
{
    public interface IHttpForwarder
    {
        /// <summary>
        /// 将请求转发到多个下游，并将第一个结果返回给请求上下文
        /// </summary>
        /// <param name="context"></param>
        /// <param name="destinations"></param>
        /// <returns></returns>
        ValueTask SendAsync(HttpContext context, Func<HttpContext, IEnumerable<string>> destinations, Func<Uri, HttpRequestMessage, ValueTask>? requestTransform = default);

        /// <summary>
        /// 将请求转发到多个下游，并将第一个结果返回给请求上下文
        /// </summary>
        /// <param name="context"></param>
        /// <param name="destinations"></param>
        /// <param name="requestTransform"></param>
        /// <returns></returns>
        ValueTask SendAsync(HttpContext context, IEnumerable<string> destinations, Func<Uri, HttpRequestMessage, ValueTask>? requestTransform = null);
    }
}
