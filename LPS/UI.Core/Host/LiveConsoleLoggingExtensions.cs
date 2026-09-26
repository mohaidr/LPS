using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Spectre.Console;

namespace LPS.UI.Core.Host
{
    internal static class LiveConsoleLoggingExtensions
    {
        internal static ILoggingBuilder AddLiveConsole(this ILoggingBuilder logging)
        {
            logging.AddConsole();
            foreach (var registration in logging.Services.Where(service =>
                service.ServiceType == typeof(ILoggerProvider) && service.ImplementationType == typeof(ConsoleLoggerProvider)).ToArray())
                logging.Services.Remove(registration);

            logging.Services.TryAddSingleton<IAnsiConsole>(AnsiConsole.Console);
            logging.Services.TryAddSingleton<ILiveConsoleOutput>(services => new LiveConsoleOutput(
                services.GetRequiredService<IAnsiConsole>(),
                AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) })));
            logging.Services.TryAddSingleton<SpectreConsoleFormatter>();
            logging.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SpectreConsoleLoggerProvider>());
            return logging;
        }
    }
}