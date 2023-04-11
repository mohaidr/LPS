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
                NumberOfClients = 1;
                RampUpPeriod= 0;
                LPSTestCases = new List<LPSTestCase.SetupCommand>();
            }

            public void Execute(LPSTestPlan entity)
            {
                entity?.Setup(this);
            }

            public List<LPSTestCase.SetupCommand> LPSTestCases { get; set; }

            public string Name { get; set; }
            public int NumberOfClients { get; set; }
            public int? RampUpPeriod { get; set; }
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
                this.IsValid = true;
                foreach (var lpsTestCaseCommand in command.LPSTestCases)
                {
                    LPSTestCases.Add(new LPSTestCase(lpsTestCaseCommand, this._logger));
                }
            }
        }  
    }
}
