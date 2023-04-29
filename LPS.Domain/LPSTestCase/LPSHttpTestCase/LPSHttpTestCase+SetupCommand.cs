using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSHttpTestCase
    {
        new public class SetupCommand : ICommand<LPSHttpTestCase>
        {

            public SetupCommand()
            {
                LPSRequest = new LPSHttpRequest.SetupCommand();
            }

            public void Execute(LPSHttpTestCase entity)
            {
                entity?.Setup(this);
            }

            public LPSHttpRequest.SetupCommand LPSRequest { get; set; }
            public LPSTestPlan.SetupCommand Plan { get; private set; }

            public int? RequestCount { get; set; }

            public int? Duration { get; set; }

            public int? BatchSize { get; set; }

            public int? CoolDownTime { get; set; }

            public bool IsValid { get; set; }

            public string Name { get; set; }

            public IterationMode? Mode
            {
                get
                {
                    if (Duration.HasValue && Duration.Value > 0
                        && CoolDownTime.HasValue && CoolDownTime.Value > 0
                        && BatchSize.HasValue && BatchSize.Value > 0
                        && !RequestCount.HasValue)
                    {
                        return IterationMode.DCB;
                    }
                    else
                    if (CoolDownTime.HasValue && CoolDownTime.Value > 0
                        && RequestCount.HasValue && RequestCount.Value > 0
                        && BatchSize.HasValue && BatchSize.Value > 0
                        && !Duration.HasValue)
                    {
                        return IterationMode.CRB;
                    }
                    else
                    if (CoolDownTime.HasValue && CoolDownTime.Value > 0
                        && BatchSize.HasValue && BatchSize.Value > 0
                        && !Duration.HasValue
                        && !RequestCount.HasValue)
                    {
                        return IterationMode.CB;
                    }
                    else
                    if (RequestCount.HasValue && RequestCount.Value > 0
                        && !Duration.HasValue
                        && !BatchSize.HasValue
                        && !CoolDownTime.HasValue)
                    {
                        return IterationMode.R;
                    }
                    else
                    if (Duration.HasValue && Duration.Value > 0
                        && !RequestCount.HasValue
                        && !BatchSize.HasValue
                        && !CoolDownTime.HasValue)
                    {
                        return IterationMode.D;
                    }

                    return null;
                }

            }

        }

        private void Setup(SetupCommand command)
        {
            _ = new Validator(this, command);

            if (command.IsValid)
            {
                this.RequestCount = command.RequestCount;
                this.Name = command.Name;
                this.Mode = command.Mode;
                ILPSClientService<LPSHttpRequest> client = null;
                if (!this.Plan.DelayClientCreationUntilIsNeeded.Value)
                {
                    ((ILPSHttpClientConfiguration<LPSHttpRequest>)_config).Timeout = TimeSpan.FromSeconds(this.Plan.ClientTimeout);
                    ((ILPSHttpClientConfiguration<LPSHttpRequest>)_config).PooledConnectionLifetime = TimeSpan.FromMinutes(this.Plan.PooledConnectionLifetime);
                    ((ILPSHttpClientConfiguration<LPSHttpRequest>)_config).PooledConnectionIdleTimeout = TimeSpan.FromMinutes(this.Plan.PooledConnectionIdleTimeout);
                    ((ILPSHttpClientConfiguration<LPSHttpRequest>)_config).MaxConnectionsPerServer = this.Plan.MaxConnectionsPerServer;
                    client = _lpsClientManager.CreateInstance(_config);
                }
                _httpClient = client;
                this.LPSHttpRequest = new LPSHttpRequest(command.LPSRequest, this._logger);
                this.Duration = command.Duration;
                this.CoolDownTime = command.CoolDownTime; ;
                this.BatchSize = command.BatchSize;
                this.IsValid = true;
            }
        }
    }
}
