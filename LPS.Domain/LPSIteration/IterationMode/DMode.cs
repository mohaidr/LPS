using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class DMode : IIterationModeService
    {
        private readonly HttpRequest.ExecuteCommand _command;
        private readonly int _duration;
        private readonly IWatchdog _watchdog;
        private readonly HttpRequest _request;
        private readonly string _hostName;
        private readonly HttpIteration _httpIteration;
        ITerminationCheckerService _terminationCheckerService;

        public DMode(
            HttpIteration httpIteration,
            HttpRequest request,
            HttpRequest.ExecuteCommand command,
            int duration,
            IWatchdog watchdog,
            ITerminationCheckerService terminationCheckerService)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _watchdog = watchdog ?? throw new ArgumentNullException(nameof(watchdog));
            _duration = duration;
            _hostName = _request.Url.HostName;
            _terminationCheckerService = terminationCheckerService;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                int numberOfSentRequests = 0;
                var stopwatch = Stopwatch.StartNew();

                while (stopwatch.Elapsed.TotalSeconds < _duration && !cancellationToken.IsCancellationRequested && !(await _terminationCheckerService.Check(_httpIteration)))
                {
                    await _watchdog.BalanceAsync(_hostName, cancellationToken);
                    await _command.ExecuteAsync(_request);
                    numberOfSentRequests++;
                }

                stopwatch.Stop();
                return numberOfSentRequests;
            }
            catch
            {
                throw;
            }
        }
    }
}
