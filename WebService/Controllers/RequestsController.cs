using Common;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebService.Controllers
{
    [Route("api/[controller]")]
    public class RequestsController : Controller
    {
        private const string RatesServiceName = "RatesService";
        private readonly StatelessServiceContext context;
        private readonly CancellationTokenSource cancellationSource;

        public RequestsController(StatelessServiceContext context, CancellationTokenSource cancellationSource)
        {
            this.context = context;
            this.cancellationSource = cancellationSource;
        }

        [HttpPost]
        [Route("")]
        public async Task<IActionResult> Post([FromBody] RateRequest request)
        {
            try
            {
                //TODO: Do some sanity checking on the request model
                ServiceEventSource.Current.ServiceMessage(
                    this.context,
                    "Received request for hotel",
                    request.HotelName);

                ServiceUriBuilder uriBuilder = new ServiceUriBuilder(RatesServiceName);
                Uri serviceUri = uriBuilder.Build();

                HttpClient httpClient = new HttpClient(new HttpServiceClientHandler());

                Uri postUrl = new HttpServiceUriBuilder()
                    .SetServiceName(serviceUri)
                    .SetPartitionKey(GetPartitionKey(request))
                    .SetServicePathAndQuery($"/api/rates")
                    .Build();

                var postData = JsonConvert.SerializeObject(request,
                                                      new JsonSerializerSettings()
                                                      {
                                                          NullValueHandling = NullValueHandling.Ignore,
                                                          Formatting = Formatting.Indented, // for readability, change to None for compactness
                                                          ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                                          DateTimeZoneHandling = DateTimeZoneHandling.Utc
                                                      });
                HttpContent httpContent = new StringContent(postData, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await httpClient.PostAsync(postUrl, httpContent, this.cancellationSource.Token);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception($"Bad status: {response.StatusCode}");

                return this.Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        private long GetPartitionKey(RateRequest request)
        {
            if (request.Country == "USA")
                return 0;
            else if (request.Country == "CAN")
                return 1;
            else if (request.Country == "AUS")
                return 2;
            else // all others
                return 3;
        }
    }
}
