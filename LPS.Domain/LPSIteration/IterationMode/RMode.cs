using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class RMode : IIterationModeService
    {
        private readonly HttpRequest.ExecuteCommand _command;
        private readonly int _requestCount;
        private readonly IWatchdog _watchdog;
        private readonly HttpRequest _request;
        private readonly HttpIteration _httpIteration;
        readonly IIterationStatusMonitor _iterationStatusMonitor;
        private readonly string _hostName;

        public RMode(
            HttpIteration httpIteration,
            HttpRequest request,
            HttpRequest.ExecuteCommand command,
            int requestCount,
            IWatchdog watchdog,
            IIterationStatusMonitor iterationStatusMonitor)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _watchdog = watchdog ?? throw new ArgumentNullException(nameof(watchdog));
            _requestCount = requestCount;
            _hostName = _request.Url.HostName;
            _iterationStatusMonitor = iterationStatusMonitor;
        }

        public async Task<int> ExecuteAsync(CancellationToken token)
        {
            try
            {
                int numberOfSentRequests = 0;
                for (int i = 0; i < _requestCount && !token.IsCancellationRequested&& !(await _iterationStatusMonitor.IsTerminatedAsync(_httpIteration, token)); i++)
                {
                    await _watchdog.BalanceAsync(_hostName, token);
                    await _command.ExecuteAsync(_request, token);
                    numberOfSentRequests++;
                }
                return numberOfSentRequests;
            }
            catch
            {
                throw;
            }
        }
    }
}
