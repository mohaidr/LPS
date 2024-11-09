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
using YamlDotNet.Serialization;

namespace LPS.Domain
{

    public partial class HttpIteration
    {
        new public class SetupCommand : ICommand<HttpIteration>, IValidCommand<HttpIteration>
        {

            public SetupCommand()
            {
                MaximizeThroughput = false;
                ValidationErrors = new Dictionary<string, List<string>>();
            }

            public void Execute(HttpIteration entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                entity?.Setup(this);
            }

            [JsonIgnore]
            [YamlIgnore]
            public Guid? Id { get; set; }
            public string Name { get; set; }
            public bool? MaximizeThroughput { get; set; }
            public IterationMode? Mode { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? RequestCount { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? Duration { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? BatchSize { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? CoolDownTime { get; set; }

            [JsonIgnore]
            [YamlIgnore]
            public bool IsValid { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonIgnore]
            [YamlIgnore]
            public IDictionary<string, List<string>> ValidationErrors { get; set; }

            public void Copy(SetupCommand targetCommand)
            {
                targetCommand.Id = this.Id;
                targetCommand.Name = this.Name;
                targetCommand.MaximizeThroughput = this.MaximizeThroughput;
                targetCommand.Mode = this.Mode;
                targetCommand.RequestCount = this.RequestCount;
                targetCommand.Duration = this.Duration;
                targetCommand.BatchSize = this.BatchSize;
                targetCommand.CoolDownTime = this.CoolDownTime;
                targetCommand.IsValid = this.IsValid;
                targetCommand.ValidationErrors = this.ValidationErrors.ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value));
            }
        }

        private void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setupcommand
            var IterationSetUpCommand = new Iteration.SetupCommand() { Id = command.Id, Name = command.Name }; // if there are fields has to be set, then pass them here.
            base.Setup(IterationSetUpCommand);
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && IterationSetUpCommand.IsValid)
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
            HttpIteration clone = new(_logger, _runtimeOperationIdProvider);
            if (this.IsValid)
            {
                clone.Id = this.Id;
                clone.Name = this.Name;
                clone.RequestCount = this.RequestCount;
                clone.Mode = this.Mode;
                clone.RequestProfile = (HttpRequestProfile)this.RequestProfile.Clone();
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
