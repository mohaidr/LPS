using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using YamlDotNet.Serialization;

namespace LPS.Domain
{
    public partial class Plan : IAggregateRoot, IValidEntity, IDomainEntity, IBusinessEntity
    {
        public class SetupCommand : ICommand<Plan>, IValidCommand<Plan>
        {
            public SetupCommand()
            {
                Rounds = [];
            }
            public string Name { get; set; }
            public IList<Round.SetupCommand> Rounds { get; set; }

            [JsonIgnore]
            [YamlIgnore]
            public Guid? Id { get; set; }
            [JsonIgnore]
            [YamlIgnore]
            public bool IsValid { get; set; }
            [JsonIgnore]
            [YamlIgnore]
            public IDictionary<string, List<string>> ValidationErrors { get; set; }

            public void Execute(Plan entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                entity?.Setup(this);
            }

            public SetupCommand Clone()
            {
                return new SetupCommand
                {
                    Id = this.Id,
                    Name = this.Name,
                    Rounds = this.Rounds.Select(r => r.Clone()).ToList(), // Assuming Round.SetupCommand also has a Clone method
                    IsValid = this.IsValid,
                    ValidationErrors = this.ValidationErrors.ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value))
                };
            }
        }
        public void AddRound(Round round)
        {
            if (round.IsValid)
            { 
                Rounds.Add(round);
            }
        }

        //TODO:Will throw exception if enumrating while modifying it,
        //TODO:will need to implement IsEnumerating flag to handle this approach
        //or change the whole approach
        //TODO: This will matter when having DB and repos
        public IEnumerable<Round> GetReadOnlyRounds()
        {
            foreach (var round in Rounds)
            {
                yield return round;
            }
        }


        private void Setup(SetupCommand command)
        {
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid)
            {
                this.Name = command.Name;
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
