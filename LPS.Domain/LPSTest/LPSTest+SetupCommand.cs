using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LPS.Domain
{

    public partial class LPSTest
    {
        public class SetupCommand: ICommand<LPSTest>
        {

            public SetupCommand()
            {
                lpsRequestWrappers = new List<LPSRequestWrapper.SetupCommand>();
            }

            public void Execute(LPSTest entity)
            {
                entity.Setup(this);
            }

            public List<LPSRequestWrapper.SetupCommand> lpsRequestWrappers { get; set; }

            public string Name { get; set; }

            public bool IsValid { get; set; }

            public bool IsCommandLine { get; set; }

        }

        private void Setup(SetupCommand dto)
        {
            new Validator(this, dto);

            if (dto.IsValid)
            {
                this.Name = dto.Name;
                this.IsValid = true;
                foreach (var command in dto.lpsRequestWrappers)
                {
                    LPSRequestWrappers.Add(new LPSRequestWrapper(command, this._logger));
                }
            }
        }  
    }
}
