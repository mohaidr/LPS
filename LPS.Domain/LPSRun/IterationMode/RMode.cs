using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class RMode : IIterationModeService
    {
        private HttpRequestProfile.ExecuteCommand _command;
        private int _requestCount;
        private IWatchdog _watchdog;
        private readonly string _hostName;
        private HttpRequestProfile _requestProfile;

        private RMode(HttpRequestProfile requestProfile)
        {
            _requestProfile = requestProfile;
            _hostName = new Uri(_requestProfile.URL).Host;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                int numberOfSentRequests = 0;
                for (int i = 0; i < _requestCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    await _watchdog.BalanceAsync(_hostName, cancellationToken);
                    await _command.ExecuteAsync(_requestProfile);
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
            private HttpRequestProfile.ExecuteCommand _command;
            private int _requestCount;
            private IWatchdog _watchdog;
            private HttpRequestProfile _requestProfile;

            public Builder SetCommand(HttpRequestProfile.ExecuteCommand command)
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

            public Builder SetRequestProfile(HttpRequestProfile requestProfile)
            {
                _requestProfile = requestProfile;
                return this;
            }

            public RMode Build()
            {
                // Validate required fields
                if (_requestProfile == null)
                    throw new InvalidOperationException("RequestProfile must be provided.");

                var rMode = new RMode(_requestProfile)
                {
                    _command = _command,
                    _requestCount = _requestCount,
                    _watchdog = _watchdog,
                    _requestProfile = _requestProfile
                };
                return rMode;
            }
        }
    }
}
