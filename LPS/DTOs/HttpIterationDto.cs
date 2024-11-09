using LPS.Domain;
using System;

namespace LPS.DTOs
{
    public class HttpIterationDto : HttpIteration.SetupCommand
    {
        public HttpRequestProfileDto RequestProfile { get; set; }

        public void DeepCopy(out HttpIterationDto targetDto)
        {
            targetDto = new HttpIterationDto();
            // Call base.Clone() and cast it to HttpIterationDto
            base.Copy(targetDto);
            HttpRequestProfileDto profileDto = new();
            // Clone RequestProfile if it's not null
            RequestProfile?.Copy(out profileDto);
            targetDto.RequestProfile = profileDto;
        }
    }

    public class ReferenceIterationDto
    {
        public string Name { get; set; }

        public void Copy(out ReferenceIterationDto targetDto)
        {
            targetDto = new ReferenceIterationDto();
            // Perform a shallow copy of the ReferenceIterationDto, as it has only a single string property.
            targetDto.Name = this.Name;
        }
    }
}
