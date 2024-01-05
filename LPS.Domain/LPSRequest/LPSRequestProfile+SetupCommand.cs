using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
    public partial class LPSRequestProfile
    {
        public class SetupCommand : ICommand<LPSRequestProfile>, IValidCommand<LPSRequestProfile>
        {
            public SetupCommand()
            {
                ValidationErrors = new Dictionary<string, List<string>>();

            }
            public Guid? Id { get; set; }
            public bool IsValid { get; set; }
            public IDictionary<string, List<string>> ValidationErrors { get; set; }

            public void Execute(LPSRequestProfile entity)
            {
                entity?.Setup(this);
            }
        }


        protected virtual void Setup(SetupCommand command)
        {
            _= new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid)
            {
                this.IsValid = true;
            }
        }

    }
}
