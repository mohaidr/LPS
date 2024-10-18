using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;

namespace LPS.Domain
{

    public partial class HttpRun
    {
        new public class SetupCommand : ICommand<HttpRun>, IValidCommand<HttpRun>
        {

            public SetupCommand()
            {
                MaximizeThroughput = false;
                LPSRequestProfile = new HttpRequestProfile.SetupCommand();
                ValidationErrors = new Dictionary<string, List<string>>();
            }

            public void Execute(HttpRun entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                entity?.Setup(this);
            }

            public HttpRequestProfile.SetupCommand LPSRequestProfile { get; set; } // only used to return data
            [JsonIgnore]
            public Guid? Id { get; set; }

            public int? RequestCount { get; set; }
            public bool? MaximizeThroughput { get; set; }
            public int? Duration { get; set; }

            public int? BatchSize { get; set; }

            public int? CoolDownTime { get; set; }
            public string Name { get; set; }
            [JsonIgnore]
            public bool IsValid { get; set; }
            public IterationMode? Mode { get; set; }
            [JsonIgnore]
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
        }

        private void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setupcommand
            var lPSRunSetUpCommand = new Run.SetupCommand() { Id = command.Id, Name = command.Name }; // if there are fields has to be set, then pass them here.
            base.Setup(lPSRunSetUpCommand);
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && lPSRunSetUpCommand.IsValid)
            {
                this.RequestCount = command.RequestCount;
                this.MaximizeThroughput = command.MaximizeThroughput.Value;
                this.Mode = command.Mode;
                this.Duration = command.Duration;
                this.CoolDownTime = command.CoolDownTime; ;
                this.BatchSize = command.BatchSize;
                this.IsValid = true;
            }
            else
            {
                this.IsValid = false;
                validator.PrintValidationErrors();
            }
        }

        public object Clone()
        {
            HttpRun clone = new HttpRun(_logger, _runtimeOperationIdProvider);
            if (this.IsValid)
            {
                clone.Id = this.Id;
                clone.Name = this.Name;
                clone.RequestCount = this.RequestCount;
                clone.Mode = this.Mode;
                clone.LPSHttpRequestProfile = (HttpRequestProfile)this.LPSHttpRequestProfile.Clone();
                clone.Duration = this.Duration;
                clone.CoolDownTime = this.CoolDownTime; ;
                clone.BatchSize = this.BatchSize;
                clone.MaximizeThroughput = this.MaximizeThroughput;
                clone.IsValid = true;
            }
            return clone;
        }
    }
}
