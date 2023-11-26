using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
        public class SetupCommand: ICommand<LPSTestPlan>, IValidCommand<LPSTestPlan>
        {

            public SetupCommand()
            {
                LPSTestCases = new List<LPSHttpTestCase.SetupCommand>();
                DelayClientCreationUntilIsNeeded = false;
                RunInParallel = false;
                ValidationErrors = new Dictionary<string, List<string>>();
            }

            public void Execute(LPSTestPlan entity)
            {
                entity?.Setup(this);
            }

            public List<LPSHttpTestCase.SetupCommand> LPSTestCases { get; set; }

            public string Name { get; set; }
            public int NumberOfClients { get; set; }
            public int RampUpPeriod { get; set; }
            public bool? DelayClientCreationUntilIsNeeded { get; set; }
            public bool? RunInParallel { get; set; }
            public bool IsValid { get; set; }
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
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
                this.RunInParallel = command.RunInParallel;
                if (!this.DelayClientCreationUntilIsNeeded.Value)
                {
                    for (int i = 0; i < this.NumberOfClients; i++)
                    {
                        _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                    }
                }
                foreach (var lpsTestCaseCommand in command.LPSTestCases)
                {
                    var lpsTestCase = new LPSHttpTestCase(lpsTestCaseCommand, this._logger, _watchdog, _runtimeOperationIdProvider);
                    LPSTestCases.Add(lpsTestCase);
                }
            }
        }  
    }
}
