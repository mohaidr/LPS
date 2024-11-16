using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LPS.Domain
{

    public partial class Round
    {
        public class SetupCommand : ICommand<Round>, IValidCommand<Round>
        {
            public SetupCommand()
            {
                DelayClientCreationUntilIsNeeded = false;
                RunInParallel = false;
                ValidationErrors = new Dictionary<string, List<string>>();
            }
            public void Execute(Round entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                entity.Setup(this);
            }
            [JsonIgnore]
            [YamlIgnore]
            public Guid? Id { get; set; }
            public virtual string Name { get; set; }
            public int StartupDelay { get; set; }
            public int? NumberOfClients { get; set; }
            public int? ArrivalDelay { get; set; }
            public bool? DelayClientCreationUntilIsNeeded { get; set; }
            public bool? RunInParallel { get; set; }

            [JsonIgnore]
            [YamlIgnore]
            public bool IsValid { get; set; }
            [JsonIgnore]
            [YamlIgnore]
            public IDictionary<string, List<string>> ValidationErrors { get; set; }

            public void Copy(SetupCommand targetCommand)
            {
                targetCommand.Id = this.Id;
                targetCommand.Name = this.Name;
                targetCommand.StartupDelay = this.StartupDelay;
                targetCommand.NumberOfClients = this.NumberOfClients;
                targetCommand.ArrivalDelay = this.ArrivalDelay;
                targetCommand.DelayClientCreationUntilIsNeeded = this.DelayClientCreationUntilIsNeeded;
                targetCommand.RunInParallel = this.RunInParallel;
                targetCommand.IsValid = this.IsValid;
                targetCommand.ValidationErrors = this.ValidationErrors.ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value));
            }
        }

        public void AddIteration(HttpIteration iteration)
        {
            if (iteration.IsValid)
            {
                Iterations.Add(iteration);
            }
        }

        //TODO:Will throw exception if enumrating while modifying it,
        //TODO:will need to implement IsEnumerating flag to handle this approach
        //or change the whole approach
        //TODO: This will matter when having DB and repos
        public IEnumerable<Iteration> GetReadOnlyIterations()
        {
            foreach (var iteration in Iterations)
            {
                yield return iteration;
            }
        }


        private void Setup(SetupCommand command)
        {
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid)
            {
                this.Name = command.Name;
                this.StartupDelay = command.StartupDelay;
                this.NumberOfClients = command.NumberOfClients.Value;
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
