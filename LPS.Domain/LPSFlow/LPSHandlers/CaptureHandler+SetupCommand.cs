using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.LPSFlow.LPSHandlers
{
        public partial class CaptureHandler : ISessionHandler
        {
        public class SetupCommand : ICommand<CaptureHandler>, IValidCommand<CaptureHandler>
        {
            public SetupCommand()
            {
                As = string.Empty;
                Regex = string.Empty;
                MakeGlobal = false;
                ValidationErrors = new Dictionary<string, List<string>>();
            }
            public string Name { get; set; }
            public string As { get; set; }
            public bool? MakeGlobal { get; set; }
            public Guid? Id { get; set; }
            public string Regex { get; set; }
            public bool IsValid { get; set; }
            public IDictionary<string, List<string>> ValidationErrors { get; set; }

            public void Execute(CaptureHandler entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                entity?.Setup(this);
            }
        }

        public object Clone()
        {
            CaptureHandler clone = new(_logger, _runtimeOperationIdProvider);
            if (this.IsValid)
            {
                clone.Id = this.Id;
                clone.Name = this.Name;
                clone.As = this.As;
                clone.Regex = this.Regex;
                clone.MakeGlobal = this.MakeGlobal;
                clone.IsValid = true;
            }
            return clone;
        }

        protected virtual void Setup(SetupCommand command)
        {
            //TODO: DeepCopy and then send the copy item instead of the original command for further protection 
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid)
            {
                this.Name = command.Name;
                this.As = command.As;
                this.MakeGlobal = command.MakeGlobal;
                this.Regex = command.Regex;
                this.IsValid = true;
            }
            else
            {
                this.IsValid = false;
                validator.PrintValidationErrors();
            }
        }
    }
}
