using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nice.HttpProxy
{
    public class HttpForwarderOptions : IOptions<HttpForwarderOptions>
    {
        /// <summary>
        /// 影子请求最大重试次数
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// 影子请求异常回调
        /// 请求地址，已重试次数，请求体，异常信息
        /// </summary>
        public Func<Uri,int,HttpRequestMessage,Exception,ValueTask>? ShadowSendExceptionHandler { get; set; }

        public HttpForwarderOptions Value => this;

    }
}
