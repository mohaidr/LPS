using System;
using AsyncTest.Domain;
using AsyncTest.Infrastructure;
using AsyncTest.UI.Core;
using Microsoft.Extensions.DependencyInjection;
using AsyncTest.Domain.Common;
using System.Threading.Tasks;
using AsyncCalls;
using Microsoft.Extensions.Hosting;

namespace AsyncTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Startup.ConfigureServices(args);
            var svc = ActivatorUtilities.CreateInstance<TestService<HttpAsyncTest.SetupCommand, HttpAsyncTest>>(Startup._host.Services);
            await svc.Run(new HttpAsyncTest.SetupCommand(), args);
        }


    }
}
