using Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WebService
{
    /// <summary>
    /// A specialized stateless service for hosting ASP.NET Core web apps.
    /// </summary>
    internal sealed class WebService : StatelessService
    {
        private readonly CancellationTokenSource _webApiCancellationSource;

        public WebService(StatelessServiceContext serviceContext)
            : base(serviceContext)
        {
            this._webApiCancellationSource = new CancellationTokenSource();
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[1]
            {
                new ServiceInstanceListener(
                    context =>
                    {
                        string tenantName = new Uri(context.CodePackageActivationContext.ApplicationName).Segments.Last();

                        return new WebHostCommunicationListener(
                            context,
                            tenantName,
                            "ServiceEndpoint",
                            uri =>
                            {
                                ServiceEventSource.Current.Message($"Listening on {uri}");

                                return new WebHostBuilder().UseWebListener()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatelessServiceContext>(context)
                                            .AddSingleton<FabricClient>(new FabricClient())
                                            .AddSingleton<CancellationTokenSource>(this._webApiCancellationSource))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseUrls(uri)
                                    .Build();
                            });
                    })
            };
        }

        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => this._webApiCancellationSource.Cancel());

            return Task.FromResult(true);
        }
    }
}
