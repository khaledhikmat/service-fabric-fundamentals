using Common;
using Common.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RatesService
{
    public class RatesService : StatefulService
    {
        internal const string ParamatersSection = "ParametersSection";
        internal const string RateCitiesDictionaryName = "store://rates/citiesdictionary";
        internal const string RateQueueName = "store://rates/queue";
        private readonly TimeSpan QueueInterval = TimeSpan.FromSeconds(10);

        private Random _random = new Random();

        private readonly CancellationTokenSource _webApiCancellationSource;

        public RatesService(StatefulServiceContext context)
            : base(context)
        {
            this._webApiCancellationSource = new CancellationTokenSource();
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[1]
            {
                new ServiceReplicaListener(
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
                                            .AddSingleton<StatefulServiceContext>(this.Context)
                                            .AddSingleton<IReliableStateManager>(this.StateManager)
                                            .AddSingleton<CancellationTokenSource>(this._webApiCancellationSource))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseUrls(uri)
                                    .Build();
                            });
                    })
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => this._webApiCancellationSource.Cancel());

            IReliableDictionary<string, RateAggregation> citiesDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, RateAggregation>>(RateCitiesDictionaryName);
            IReliableQueue<RateRequest> queue = await this.StateManager.GetOrAddAsync<IReliableQueue<RateRequest>>(RateQueueName);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var tx = this.StateManager.CreateTransaction())
                    {
                        var result = await queue.TryDequeueAsync(tx);

                        if (result.HasValue)
                        {
                            RateRequest request = result.Value;

                            // TODO: Process the request
                            // TODO: Go against the reservation provider to pick up the rate
                            // TODO: Determine the reservation provider per tenant from the configuration parameters
                            string providerName = GetParameterValue(ParamatersSection, "ProviderName");
                            int nights = (request.CheckOutDate - request.CheckInDate).Days;
                            int netAmount = _random.Next(500) * nights;
                            var newAggregation = new RateAggregation();
                            newAggregation.Transactions = 1;
                            newAggregation.Nights = nights;
                            newAggregation.Amount = (double) netAmount;

                            await citiesDictionary.AddOrUpdateAsync(tx, $"{request.City}/{request.Country}/{providerName}", newAggregation, (key, currentValue) =>
                            {
                                currentValue.Transactions += newAggregation.Transactions;
                                currentValue.Nights += newAggregation.Nights;
                                currentValue.Amount += newAggregation.Amount;
                                return currentValue;
                            });

                            // This commits the add to dictionary and the dequeue operation.
                            await tx.CommitAsync();
                        }
                    }
                }
                catch (Exception e)
                {

                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        private string GetParameterValue(string section, string parameterKey)
        {
            try
            {
                var parameterValue = "";
                var configurationPackage = Context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
                if (configurationPackage != null)
                {
                    var configSection = configurationPackage.Settings.Sections[section];
                    if (configSection != null)
                    {
                        var connectorParameter = configSection.Parameters[parameterKey];
                        if (connectorParameter != null)
                        {
                            parameterValue = connectorParameter.Value;
                        }
                    }
                }

                return parameterValue;
            }
            catch (Exception ex)
            {
                return "";
            }
        }
    }
}
