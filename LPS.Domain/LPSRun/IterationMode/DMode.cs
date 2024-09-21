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
        private HttpRequestProfile.ExecuteCommand _command;
        private int _duration;
        private IWatchdog _watchdog;
        private readonly string _hostName;
        private HttpRequestProfile _requestProfile;

        private DMode(HttpRequestProfile requestProfile)
        {
            _requestProfile = requestProfile;
            _hostName = new Uri(_requestProfile.URL).Host;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            int numberOfSentRequests = 0;
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalSeconds < _duration && !cancellationToken.IsCancellationRequested)
            {
                await _watchdog.BalanceAsync(_hostName, cancellationToken);
                await _command.ExecuteAsync(_requestProfile);
                numberOfSentRequests++;
            }
            stopwatch.Stop();
            return numberOfSentRequests;
        }

        public class Builder : IBuilder<DMode>
        {
            private HttpRequestProfile.ExecuteCommand _command;
            private int _duration;
            private IWatchdog _watchdog;
            private HttpRequestProfile _requestProfile;

            public Builder SetCommand(HttpRequestProfile.ExecuteCommand command)
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

            public Builder SetRequestProfile(HttpRequestProfile requestProfile)
            {
                _requestProfile = requestProfile;
                return this;
            }

            public DMode Build()
            {
                if (_requestProfile == null)
                    throw new InvalidOperationException("RequestProfile must be provided.");

                var dMode = new DMode(_requestProfile)
                {
                    _command = _command,
                    _duration = _duration,
                    _watchdog = _watchdog,
                    _requestProfile = _requestProfile
                };
                return dMode;
            }
        }
    }
}
