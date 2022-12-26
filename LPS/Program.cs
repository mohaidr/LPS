using System;
using LPS.Domain;
using Microsoft.Extensions.DependencyInjection;
using LPS.Domain.Common;
using System.Threading.Tasks;
using LPS;
using Microsoft.Extensions.Hosting;

namespace LPS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("====================================================");
            Console.WriteLine("LPS Testing Tool V1");
            Console.WriteLine("====================================================");
            await Startup.ConfigureServices(args);
        }


    }
}
