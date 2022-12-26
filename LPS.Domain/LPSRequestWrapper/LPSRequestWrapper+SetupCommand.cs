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

    public partial class LPSRequestWrapper
    {
        public class SetupCommand: ICommand<LPSRequestWrapper>
        {

            public SetupCommand()
            {
                LPSRequest = new LPSRequest.SetupCommand();
            }

            public void Execute(LPSRequestWrapper entity)
            {
                entity.Setup(this);
            }

            public LPSRequest.SetupCommand LPSRequest { get; set; }

            public int NumberofAsyncRepeats { get; set; }

            public bool IsValid { get; set; }

            public string Name { get; set; }
        }

        private void Setup(SetupCommand dto)
        {
            new Validator(this, dto);

            if (dto.IsValid)
            {

                this.NumberofAsyncRepeats = dto.NumberofAsyncRepeats;
                this.Name = dto.Name;
                this.LPSRequest = new LPSRequest(dto.LPSRequest, this._logger);
                this.IsValid = true;
            }
        }
    }
}
