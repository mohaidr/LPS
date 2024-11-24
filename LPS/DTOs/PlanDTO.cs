using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LPS.DTOs
{
    public class PlanDto : Plan.SetupCommand
    {
        public PlanDto()
        {
            Iterations = [];
            Rounds = [];
        }
        public override string Name { get; set; }
        public IList<RoundDto> Rounds { get; set; }
        public IList<VariableDto> Variables { get; set; }
        public IList<HttpIterationDto> Iterations { get; set; }
        public void DeepCopy(out PlanDto targetDto)
        {
            targetDto = new PlanDto();

            // Call base.Clone() and cast it to PlanDto
            base.Copy(targetDto);

            #pragma warning disable CS8601 // Possible null reference assignment.
            targetDto.Iterations = Iterations?.Select(iteration =>
            {
                var copiedIteration = new HttpIterationDto();
                iteration.Copy(copiedIteration);
                return copiedIteration;
            }).ToList();

            #pragma warning disable CS8601 // Possible null reference assignment.
            targetDto.Rounds = Rounds?.Select(round =>
            {
                var copiedRound = new RoundDto();
                round.Copy(copiedRound); 
                return copiedRound;
            }).ToList();
        }
    }

    public class VariableDto
    {
        public VariableDto()
        {
            As = "Text";
            Regex = string.Empty;
        }
        public string Name { get; set; }
        public string Value { get; set; }
        public string As { get; set; }
        public string Regex { get; set; }
    }
}
