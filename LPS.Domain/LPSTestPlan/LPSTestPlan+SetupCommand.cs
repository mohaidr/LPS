using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
        public class SetupCommand: ICommand<LPSTestPlan>
        {

            public SetupCommand()
            {
                //NumberOfClients = 1;
                //RampUpPeriod= 0;
                LPSTestCases = new List<LPSHttpTestCase.SetupCommand>();
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
        }

        private void Setup(SetupCommand command)
        {
            new Validator(this, command);

            if (command.IsValid)
            {
                this.Name = command.Name;
                this.NumberOfClients= command.NumberOfClients;
                this.RampUpPeriod= command.RampUpPeriod;
                this.DelayClientCreationUntilIsNeeded = command.DelayClientCreationUntilIsNeeded;
                this.IsValid = true;
                this.MaxConnectionsPerServer= command.MaxConnectionsPerServer;
                this.PooledConnectionLifetime= command.PooledConnectionLifetime;
                this.PooledConnectionIdleTimeout= command.PooledConnectionIdleTimeout;
                this.ClientTimeout= command.ClientTimeout;
                foreach (var lpsTestCaseCommand in command.LPSTestCases)
                {
                    var lpsTestCase = new LPSHttpTestCase(_lpsClientManager, _config, this._logger);
                    lpsTestCase.Plan= this;
                    lpsTestCaseCommand.Execute(lpsTestCase);
                    LPSTestCases.Add(lpsTestCase);
                }
            }
        }  
    }
}
