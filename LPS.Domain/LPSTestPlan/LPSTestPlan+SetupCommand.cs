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
                Name =  DateTime.Now.ToFileTime().ToString();
                LPSTestCases = new List<LPSTestCase.SetupCommand>();
            }

            public void Execute(LPSTestPlan entity)
            {
                entity?.Setup(this);
            }

            public List<LPSTestCase.SetupCommand> LPSTestCases { get; set; }

            public string Name { get; set; }

            public bool IsValid { get; set; }
        }

        private void Setup(SetupCommand dto)
        {
            new Validator(this, dto);

            if (dto.IsValid)
            {
                this.Name = dto.Name;
                this.IsValid = true;
                foreach (var command in dto.LPSTestCases)
                {
                    LPSTestCases.Add(new LPSTestCase(command, this._logger));
                }
            }
        }  
    }
}
