using System;
using AsyncTest.Domain;
using AsyncTest.Infrastructure;
using AsyncTest.UI.Core;
using Microsoft.Extensions.DependencyInjection;
using AsyncTest.Domain.Common;
using System.Threading.Tasks;

namespace AsyncTest
{
    class Program
    {
        private static IServiceProvider serviceProvider;

        static async Task Main(string[] args)
        {
            ConfigureServices();
            await StartAsyncTest(args);
        }

        public static void ConfigureServices()
        {
            serviceProvider = new ServiceCollection()
           .AddLogging()
           .AddTransient<IFileLogger, FileLogger>()
           .BuildServiceProvider();
        }

        public async static Task StartAsyncTest(string[] args)
        {
            HttpAsyncTest.SetupCommand setupCommand = new HttpAsyncTest.SetupCommand();

            if (args.Length != 0)
            {
                TestService.BuildFromCommand(setupCommand, args);
            }
            else
            {
                Console.WriteLine("Start building your test manually");
                TestService.BuildManual(setupCommand);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has started...");
            Console.ResetColor();

            var logger = serviceProvider.GetService<IFileLogger>();
            HttpAsyncTest asyncTest = new HttpAsyncTest(setupCommand, logger);
            await new HttpAsyncTest.ExecuteCommand().ExecuteAsync(asyncTest);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has completed...");
            Console.ResetColor();
            string action;
            while (true)
            {
                Console.WriteLine("Press any key to exit, enter \"start over\" to start over or \"redo\" to repeat the same test ");
                action = Console.ReadLine().Trim().ToLower();
                if (action == "redo")
                {
                    await new HttpAsyncTest.RedoCommand().ExecuteAsync(asyncTest);
                    continue;
                }
                break;
            }
            if (action == "start over")
            {
                args = new string[] { };
                await StartAsyncTest(args);
            }
        }
    }
}
