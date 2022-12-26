using System;
using AsyncTest.Domain;
using AsyncTest.Infrastructure;
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
            Console.WriteLine("====================================================");
            Console.WriteLine("LPS Tool V1");
            Console.WriteLine("====================================================");
            await Startup.ConfigureServices(args);
        }


    }
}
