using Microsoft.AspNetCore.Hosting;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RatesService
{
    public class Program
    {
        // Entry point for the application.
        public static void Main(string[] args)
        {
            ServiceRuntime.RegisterServiceAsync(
                "RatesServiceType",
                context =>
                    new RatesService(context)).GetAwaiter().GetResult();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
