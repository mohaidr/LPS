using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class DMode : IIterationModeService
    {
        private HttpSession.ExecuteCommand _command;
        private int _duration;
        private IWatchdog _watchdog;
        private readonly string _hostName;
        private HttpSession _session;

        private DMode(HttpSession session)
        {
            _session = session;
            _hostName = new Uri(_session.URL).Host;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                int numberOfSentRequests = 0;
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed.TotalSeconds < _duration && !cancellationToken.IsCancellationRequested)
                {
                    await _watchdog.BalanceAsync(_hostName, cancellationToken);
                    await _command.ExecuteAsync(_session);
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

        public class Builder : IBuilder<DMode>
        {
            private HttpSession.ExecuteCommand _command;
            private int _duration;
            private IWatchdog _watchdog;
            private HttpSession _session;

            public Builder SetCommand(HttpSession.ExecuteCommand command)
            {
                _command = command;
                return this;
            }

            public Builder SetDuration(int duration)
            {
                _duration = duration;
                return this;
            }

            public Builder SetWatchdog(IWatchdog watchdog)
            {
                _watchdog = watchdog;
                return this;
            }

            public Builder SetSession(HttpSession session)
            {
                _session = session;
                return this;
            }

            public DMode Build()
            {
                if (_session == null)
                    throw new InvalidOperationException("Session must be provided.");

                var dMode = new DMode(_session)
                {
                    _command = _command,
                    _duration = _duration,
                    _watchdog = _watchdog,
                    _session = _session
                };
                return dMode;
            }
        }
    }
}
