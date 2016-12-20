using Common;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WebService.Controllers
{
    [Route("api/[controller]")]
    public class StatsController : Controller
    {
        private const string RatesServiceName = "RatesService";
        private readonly FabricClient fabricClient;
        private readonly CancellationTokenSource cancellationSource;

        public StatsController(FabricClient fabricClient, CancellationTokenSource cancellationSource)
        {
            this.fabricClient = fabricClient;
            this.cancellationSource = cancellationSource;
        }

        [HttpGet]
        [Route("queue/length")]
        public async Task<IActionResult> GetQueueLengthAsync()
        {
            ServiceUriBuilder uriBuilder = new ServiceUriBuilder(RatesServiceName);
            Uri serviceUri = uriBuilder.Build();

            // service may be partitioned.
            // this will aggregate the queue lengths from each partition
            ServicePartitionList partitions = await this.fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

            HttpClient httpClient = new HttpClient(new HttpServiceClientHandler());

            long count = 0;
            foreach (Partition partition in partitions)
            {
                Uri getUrl = new HttpServiceUriBuilder()
                    .SetServiceName(serviceUri)
                    .SetPartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey)
                    .SetServicePathAndQuery($"/api/rates/queue/length")
                    .Build();

                HttpResponseMessage response = await httpClient.GetAsync(getUrl, this.cancellationSource.Token);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return this.StatusCode((int)response.StatusCode);
                }

                string result = await response.Content.ReadAsStringAsync();

                count += Int64.Parse(result);
            }

            return this.Ok(count);
        }

        [HttpGet]
        [Route("cities")]
        public async Task<IActionResult> GetCitiesAsync()
        {
            ServiceUriBuilder uriBuilder = new ServiceUriBuilder(RatesServiceName);
            Uri serviceUri = uriBuilder.Build();

            // service may be partitioned.
            // this will aggregate cities from all partitions
            ServicePartitionList partitions = await this.fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

            HttpClient httpClient = new HttpClient(new HttpServiceClientHandler());

            List<CityStats> cities = new List<CityStats>();
            foreach (Partition partition in partitions)
            {
                Uri getUrl = new HttpServiceUriBuilder()
                    .SetServiceName(serviceUri)
                    .SetPartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey)
                    .SetServicePathAndQuery($"/api/rates/cities")
                    .Build();

                HttpResponseMessage response = await httpClient.GetAsync(getUrl, this.cancellationSource.Token);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return this.StatusCode((int)response.StatusCode);
                }

                JsonSerializer serializer = new JsonSerializer();
                using (StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                {
                    using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                    {
                        List<CityStats> result = serializer.Deserialize<List<CityStats>>(jsonReader);

                        if (result != null)
                        {
                            cities.AddRange(result);
                        }
                    }
                }
            }

            return this.Ok(cities);
        }
    }
}
