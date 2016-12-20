﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Common
{
    using System;
    using System.Fabric;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Services.Client;
    using Newtonsoft.Json.Linq;

    // Borrowed from: https://github.com/Azure-Samples/service-fabric-dotnet-iot
    public class HttpServiceClientHandler : HttpClientHandler
    {
        private const int MaxRetries = 5;
        private const int InitialRetryDelayMs = 25;
        private readonly Random random = new Random();

        public HttpServiceClientHandler()
        {
        }

        /// <summary>
        /// http://fabric/app/service/#/partitionkey/any|primary|secondary/endpoint-name/api-path
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ServicePartitionResolver resolver = ServicePartitionResolver.GetDefault();
            ResolvedServicePartition partition = null;
            HttpServiceUriBuilder uriBuilder = new HttpServiceUriBuilder(request.RequestUri);

            int retries = MaxRetries;
            int retryDelay = InitialRetryDelayMs;
            bool resolveAddress = true;

            HttpResponseMessage lastResponse = null;
            Exception lastException = null;

            while (retries --> 0)
            {
                lastResponse = null;
                cancellationToken.ThrowIfCancellationRequested();

                if (resolveAddress)
                {
                    partition = partition != null
                        ? await resolver.ResolveAsync(partition, cancellationToken)
                        : await resolver.ResolveAsync(uriBuilder.ServiceName, uriBuilder.PartitionKey, cancellationToken);

                    string serviceEndpointJson;

                    switch (uriBuilder.Target)
                    {
                        case HttpServiceUriTarget.Default:
                        case HttpServiceUriTarget.Primary:
                            serviceEndpointJson = partition.GetEndpoint().Address;
                            break;
                        case HttpServiceUriTarget.Secondary:
                            serviceEndpointJson = partition.Endpoints.ElementAt(this.random.Next(1, partition.Endpoints.Count)).Address;
                            break;
                        case HttpServiceUriTarget.Any:
                        default:
                            serviceEndpointJson = partition.Endpoints.ElementAt(this.random.Next(0, partition.Endpoints.Count)).Address;
                            break;
                    }

                    string endpointUrl = JObject.Parse(serviceEndpointJson)["Endpoints"][uriBuilder.EndpointName].Value<string>();

                    request.RequestUri = new Uri($"{endpointUrl.TrimEnd('/')}/{uriBuilder.ServicePathAndQuery.TrimStart('/')}", UriKind.Absolute);
                }

                try
                {
                    lastResponse = await base.SendAsync(request, cancellationToken);

                    if (lastResponse.StatusCode == HttpStatusCode.NotFound ||
                        lastResponse.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        resolveAddress = true;
                    }
                    else
                    {
                        return lastResponse;
                    }
                }
                catch (TimeoutException te)
                {
                    lastException = te;
                    resolveAddress = true;
                }
                catch (SocketException se)
                {
                    lastException = se;
                    resolveAddress = true;
                }
                catch (HttpRequestException hre)
                {
                    lastException = hre;
                    resolveAddress = true;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    WebException we = ex as WebException;

                    if (we == null)
                    {
                        we = ex.InnerException as WebException;
                    }

                    if (we != null)
                    {
                        HttpWebResponse errorResponse = we.Response as HttpWebResponse;

                        // the following assumes port sharing
                        // where a port is shared by multiple replicas within a host process using a single web host (e.g., http.sys).
                        if (we.Status == WebExceptionStatus.ProtocolError)
                        {
                            if (errorResponse.StatusCode == HttpStatusCode.NotFound ||
                                errorResponse.StatusCode == HttpStatusCode.ServiceUnavailable)
                            {
                                // This could either mean we requested an endpoint that does not exist in the service API (a user error)
                                // or the address that was resolved by fabric client is stale (transient runtime error) in which we should re-resolve.
                                resolveAddress = true;
                            }

                            // On any other HTTP status codes, re-throw the exception to the caller.
                            throw;
                        }

                        if (we.Status == WebExceptionStatus.Timeout ||
                            we.Status == WebExceptionStatus.RequestCanceled ||
                            we.Status == WebExceptionStatus.ConnectionClosed ||
                            we.Status == WebExceptionStatus.ConnectFailure)
                        {
                            resolveAddress = true;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }

                await Task.Delay(retryDelay);

                retryDelay += retryDelay;
            }

            if (lastResponse != null)
            {
                return lastResponse;
            }
            else
            {
                throw lastException;
            }
        }
    }
}