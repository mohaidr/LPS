using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LPS.Domain
{

    public partial class TestPlan
    {
        public class SetupCommand: ICommand<TestPlan>, IValidCommand<TestPlan>
        {
            public IList<HttpRun.SetupCommand> LPSRuns { get; set; } // only used to return data
            public SetupCommand()
            {
                LPSRuns = new List<HttpRun.SetupCommand>();
                DelayClientCreationUntilIsNeeded = false;
                RunInParallel = false;
                ValidationErrors = new Dictionary<string, List<string>>();
            }

            public void Execute(TestPlan entity)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity));
                }
                entity?.Setup(this);
            }
            public Guid? Id { get; set; }
            public string Name { get; set; }
            public int NumberOfClients { get; set; }
            public int ArrivalDelay { get; set; }
            public bool? DelayClientCreationUntilIsNeeded { get; set; }
            public bool? RunInParallel { get; set; }
            public bool IsValid { get; set; }
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
        }

        private void Setup(SetupCommand command)
        {
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid)
            {
                this.Name = command.Name;
                this.NumberOfClients= command.NumberOfClients;
                this.ArrivalDelay = command.ArrivalDelay;
                this.DelayClientCreationUntilIsNeeded = command.DelayClientCreationUntilIsNeeded;
                this.IsValid = true;
                this.RunInParallel = command.RunInParallel;
            }
            else
            {
                this.IsValid = false;
                validator.PrintValidationErrors();
            }
        }  
    }
}
