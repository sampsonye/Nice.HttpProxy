using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nice.HttpProxy
{
    internal class ShadowRequestStruct
    {
        public HttpRequestMessage HttpRequestMessage { get; set; } = default!;
        public List<UrlRetryTimes> FullUrls { get; set; } = default!;
        public Func<Uri, HttpRequestMessage, ValueTask>? RequestTransform { get; set; }
    }

    internal class UrlRetryTimes
    {
        public int Retries { get; set; }
        public Uri FullUrl { get; set; } = default!;
    }
}
