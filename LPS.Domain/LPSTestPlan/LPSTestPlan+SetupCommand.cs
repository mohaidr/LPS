using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
        public class SetupCommand: ICommand<LPSTestPlan>, IValidCommand
        {

            public SetupCommand()
            {
                LPSTestCases = new List<LPSHttpTestCase.SetupCommand>();
                ClientTimeout = 240;
                PooledConnectionIdleTimeout = 5;
                PooledConnectionLifetime = 25;
            }

            public void Execute(LPSTestPlan entity)
            {
                entity?.Setup(this);
            }

            public List<LPSHttpTestCase.SetupCommand> LPSTestCases { get; set; }

            public string Name { get; set; }
            public int NumberOfClients { get; set; }
            public int RampUpPeriod { get; set; }
            public int ClientTimeout { get; set; }
            public int PooledConnectionLifetime { get; set; }
            public int PooledConnectionIdleTimeout { get; set; }
            public int MaxConnectionsPerServer { get; set; }
            public bool? DelayClientCreationUntilIsNeeded { get; set; }
            public bool IsValid { get; set; }
            public IDictionary<string, string> ValidationErrors { get; set; }
        }

        private void Setup(SetupCommand command)
        {
            new Validator(this, command, _logger, _runtimeOperationIdProvider);

            if (command.IsValid)
            {
                this.Name = command.Name;
                this.NumberOfClients= command.NumberOfClients;
                this.RampUpPeriod= command.RampUpPeriod;
                this.DelayClientCreationUntilIsNeeded = command.DelayClientCreationUntilIsNeeded;
                this.IsValid = true;
                this.MaxConnectionsPerServer= command.MaxConnectionsPerServer;
                this.PooledConnectionLifeTime= command.PooledConnectionLifetime;
                this.PooledConnectionIdleTimeout= command.PooledConnectionIdleTimeout;
                this.ClientTimeout= command.ClientTimeout;
                ((ILPSHttpClientConfiguration<LPSHttpRequest>)_lpsClientConfig).Timeout = TimeSpan.FromSeconds(this.ClientTimeout);
                ((ILPSHttpClientConfiguration<LPSHttpRequest>)_lpsClientConfig).PooledConnectionLifetime = TimeSpan.FromMinutes(this.PooledConnectionLifeTime);
                ((ILPSHttpClientConfiguration<LPSHttpRequest>)_lpsClientConfig).PooledConnectionIdleTimeout = TimeSpan.FromMinutes(this.PooledConnectionIdleTimeout);
                ((ILPSHttpClientConfiguration<LPSHttpRequest>)_lpsClientConfig).MaxConnectionsPerServer = this.MaxConnectionsPerServer;
                if (!this.DelayClientCreationUntilIsNeeded.Value)
                {
                    for (int i = 0; i < this.NumberOfClients; i++)
                    {
                        _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                    }
                }
                foreach (var lpsTestCaseCommand in command.LPSTestCases)
                {
                    var lpsTestCase = new LPSHttpTestCase(this._logger, _runtimeOperationIdProvider);
                    lpsTestCase.Plan= this;
                    lpsTestCaseCommand.Execute(lpsTestCase);
                    LPSTestCases.Add(lpsTestCase);
                }
            }
        }  
    }
}
