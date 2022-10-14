using Nice.HttpProxy;
using Nice.HttpProxy.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class HttpForwarderCollectionExtensions
    {
        public static IServiceCollection AddHttpForwarder(this IServiceCollection services,Action<HttpForwarderOptions>? configure=default) {
            //var opts = new HttpForwarderOptions();
            //configure?.Invoke(opts);
            //services.ConfigureOptions(opts);
            services.Configure<HttpForwarderOptions>(configure);

            services.AddSingleton<IHttpForwarder,DefaultHttpForwarder>();

            return services;
        }
    }
}
