using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class RMode : IIterationModeService
    {
        private HttpSession.ExecuteCommand _command;
        private int _requestCount;
        private IWatchdog _watchdog;
        private readonly string _hostName;
        private HttpSession _session;

        private RMode(HttpSession session)
        {
            _session = session;
            _hostName = new Uri(_session.URL).Host;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                int numberOfSentRequests = 0;
                for (int i = 0; i < _requestCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    await _watchdog.BalanceAsync(_hostName, cancellationToken);
                    await _command.ExecuteAsync(_session);
                    numberOfSentRequests++;
                }
                return numberOfSentRequests;
            }
            catch 
            {
                throw;
            }
        }
        public class Builder : IBuilder<RMode>
        {
            private HttpSession.ExecuteCommand _command;
            private int _requestCount;
            private IWatchdog _watchdog;
            private HttpSession _session;

            public Builder SetCommand(HttpSession.ExecuteCommand command)
            {
                _command = command;
                return this;
            }

            public Builder SetRequestCount(int requestCount)
            {
                _requestCount = requestCount;
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

            public RMode Build()
            {
                // Validate required fields
                if (_session == null)
                    throw new InvalidOperationException("Session must be provided.");

                var rMode = new RMode(_session)
                {
                    _command = _command,
                    _requestCount = _requestCount,
                    _watchdog = _watchdog,
                    _session = _session
                };
                return rMode;
            }
        }
    }
}
