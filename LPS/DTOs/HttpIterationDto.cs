using LPS.Domain;
using System;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace LPS.DTOs
{
    public class HttpIterationDto : HttpIteration.SetupCommand
    {
        public HttpIterationDto() {
            Session = new();
        }
        public override string Name{get; set;}
        public HttpSessionDto Session { get; set; }

        public void DeepCopy(out HttpIterationDto targetDto)
        {
            targetDto = new HttpIterationDto();
            // Call base.Clone() and cast it to HttpIterationDto
            base.Copy(targetDto);
            HttpSessionDto SessionDto = new();
            // Clone Session if it's not null
            Session?.DeepCopy(out SessionDto);
            targetDto.Session = SessionDto;
        }
    }

    public class ReferenceIterationDto
    {
        public string Name { get; set; }
        public void DeepCopy(out ReferenceIterationDto targetDto)
        {
            targetDto = new ReferenceIterationDto
            {
                // Perform a shallow copy of the ReferenceIterationDto, as it has only a single string property.
                Name = this.Name
            };
        }
    }
}
