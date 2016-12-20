using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace RatesService.Controllers
{
    [Route("api/[controller]")]
    public class RatesController : Controller
    {
        private readonly IReliableStateManager stateManager;
        private readonly StatefulServiceContext context;
        private readonly CancellationTokenSource serviceCancellationSource;

        public RatesController(IReliableStateManager stateManager, StatefulServiceContext context, CancellationTokenSource serviceCancellationSource)
        {
            this.stateManager = stateManager;
            this.context = context;
            this.serviceCancellationSource = serviceCancellationSource;
        }

        [HttpGet]
        [Route("queue/length")]
        public async Task<IActionResult> GetQueueLengthAsync()
        {
            IReliableQueue<RateRequest> queue =
                await this.stateManager.GetOrAddAsync<IReliableQueue<RateRequest>>(RatesService.RateQueueName);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                long count = await queue.GetCountAsync(tx);

                return this.Ok(count);
            }
        }

        [HttpGet]
        [Route("cities")]
        public async Task<IActionResult> GetCitiesAsync()
        {
            IReliableDictionary<string, RateAggregation> store = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, RateAggregation>>(RatesService.RateCitiesDictionaryName);

            List<CityStats> cities = new List<CityStats>();
            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<string, RateAggregation>> enumerable = await store.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<string, RateAggregation>> enumerator = enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(this.serviceCancellationSource.Token))
                {
                    cities.Add(
                        new CityStats()
                        {
                            Name = enumerator.Current.Key,
                            Transactions = enumerator.Current.Value.Transactions,
                            Nights = enumerator.Current.Value.Nights,
                            Amount = enumerator.Current.Value.Amount
                        });
                }
            }

            return this.Ok(cities);
        }

        [HttpPost]
        [Route("")]
        public async Task<IActionResult> Post([FromBody] RateRequest request)
        {
            try
            {
                //TODO: Do some snaity checking on the request model
                ServiceEventSource.Current.ServiceMessage(
                    this.context,
                    "Received request for hotel",
                    request.HotelName);

                IReliableQueue<RateRequest> queue = await this.stateManager.GetOrAddAsync<IReliableQueue<RateRequest>>(RatesService.RateQueueName);

                using (ITransaction tx = this.stateManager.CreateTransaction())
                {
                    // Queue the request
                    await queue.EnqueueAsync(tx, request);

                    // Commit
                    await tx.CommitAsync();
                }

                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
