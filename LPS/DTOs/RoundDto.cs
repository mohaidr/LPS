using LPS.Domain;
using LPS.Infrastructure.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LPS.DTOs
{
    public class RoundDto : Round.SetupCommand
    {
        public RoundDto() 
        {
            Iterations = [];
            ReferencedIterations = [];
            BaseUrl = string.Empty;
        }
        public override string Name { get; set; }
        public string BaseUrl { get; set; }

        public List<HttpIterationDto> Iterations { get; set; } // Inline iterations
        [JsonPropertyName("ref")]
        [YamlMember(Alias = "ref")]
        public List<ReferenceIterationDto> ReferencedIterations { get; set; } // Referenced iterations
        public void DeepCopy(out RoundDto targetDto)
        {
            targetDto = new RoundDto();
            // Call base.Clone() and cast it to RoundDto
            base.Copy(targetDto);
            targetDto.BaseUrl = BaseUrl;
            // Clone Iterations list if it's not null
            #pragma warning disable CS8601 // Possible null reference assignment.
            targetDto.Iterations = Iterations?.Select(iteration =>
            {
                var copiedIteration = new HttpIterationDto();
                iteration.DeepCopy(out copiedIteration);
                return copiedIteration;
            }).ToList();

            #pragma warning disable CS8601 // Possible null reference assignment.
            targetDto.ReferencedIterations = ReferencedIterations?.Select(iteration =>
            {
                var copiedIteration = new ReferenceIterationDto();
                iteration.DeepCopy(out copiedIteration);
                return copiedIteration;
            }).ToList();
        }
    }

}
