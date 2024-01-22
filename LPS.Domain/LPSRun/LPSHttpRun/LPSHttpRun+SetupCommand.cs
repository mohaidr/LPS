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
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class LPSHttpRun
    {
        new public class SetupCommand : ICommand<LPSHttpRun>, IValidCommand<LPSHttpRun>
        {

            public SetupCommand()
            {
                LPSRequestProfile = new LPSHttpRequestProfile.SetupCommand();
                ValidationErrors = new Dictionary<string, List<string>>();
            }

            public void Execute(LPSHttpRun entity)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity));
                }
                entity?.Setup(this);
            }

            public LPSHttpRequestProfile.SetupCommand LPSRequestProfile { get; set; } // only used to return data
            public Guid? Id { get; set; }

            public int? RequestCount { get; set; }

            public int? Duration { get; set; }

            public int? BatchSize { get; set; }

            public int? CoolDownTime { get; set; }
            public string Name { get; set; }
            public bool IsValid { get; set; }
            public IterationMode? Mode { get; set; }
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
        }

        private void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setupcommand
            var lPSRunSetUpCommand = new LPSRun.SetupCommand() { Id = command.Id, Name = command.Name }; // if there are fields has to be set, then pass thm here.
            base.Setup(lPSRunSetUpCommand);
            new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && lPSRunSetUpCommand.IsValid)
            {
                this.RequestCount = command.RequestCount;
                this.Mode = command.Mode;
                this.Duration = command.Duration;
                this.CoolDownTime = command.CoolDownTime; ;
                this.BatchSize = command.BatchSize;
                this.IsValid = true;
            }
        }

        public object Clone()
        {
            LPSHttpRun clone = new LPSHttpRun(_logger, _runtimeOperationIdProvider);
            if (this.IsValid)
            {
                clone.Id = this.Id;
                clone.Name = this.Name;
                clone.RequestCount = this.RequestCount;
                clone.Mode = this.Mode;
                clone.LPSHttpRequestProfile = (LPSHttpRequestProfile)this.LPSHttpRequestProfile.Clone();
                clone.Duration = this.Duration;
                clone.CoolDownTime = this.CoolDownTime; ;
                clone.BatchSize = this.BatchSize;
                clone.IsValid = true;
            }
            return clone;
        }
    }
}
