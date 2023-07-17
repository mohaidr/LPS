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

    public partial class LPSRequest
    {
        public class SetupCommand : ICommand<LPSRequest>, IValidCommand
        {
            public SetupCommand()
            {
            }

            public bool IsValid { get; set; }
            public IDictionary<string, string> ValidationErrors { get; set; }

            public void Execute(LPSRequest entity)
            {
                entity?.Setup(this);
            }
        }


        protected virtual void Setup(SetupCommand command)
        {
            _= new Validator(this, command, _logger);
            if (command.IsValid)
            {
                this.IsValid = true;
            }
        }

    }
}
