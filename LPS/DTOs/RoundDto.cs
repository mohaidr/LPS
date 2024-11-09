using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.DTOs
{
    public class RoundDto : Round.SetupCommand
    {
        public List<HttpIterationDto> Iterations { get; set; } // Inline iterations
        public List<ReferenceIterationDto> ReferencedIterations { get; set; } // Referenced iterations

        public void DeepCopy(out RoundDto targetDto)
        {
            targetDto = new RoundDto();
            // Call base.Clone() and cast it to RoundDto
            base.Copy(targetDto);

            // Clone Iterations list if it's not null
            #pragma warning disable CS8601 // Possible null reference assignment.
            targetDto.Iterations = Iterations?.Select(iteration =>
            {
                var copiedIteration = new HttpIterationDto();
                iteration.Copy(copiedIteration);
                return copiedIteration;
            }).ToList();

            #pragma warning disable CS8601 // Possible null reference assignment.
            targetDto.ReferencedIterations = ReferencedIterations?.Select(iteration =>
            {
                var copiedIteration = new ReferenceIterationDto();
                iteration.Copy(out copiedIteration);
                return copiedIteration;
            }).ToList();
        }
    }

}
