using LPS.Domain;
using System;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace LPS.DTOs
{
    public class HttpIterationDto : HttpIteration.SetupCommand
    {
        public HttpIterationDto() {
            HttpRequest = new();
        }
        public override string Name{get; set;}
        public HttpRequestDto HttpRequest { get; set; }

        public void DeepCopy(out HttpIterationDto targetDto)
        {
            targetDto = new HttpIterationDto();
            // Call base.Clone() and cast it to HttpIterationDto
            base.Copy(targetDto);
            HttpRequestDto RequestDto = new();
            // Clone Request if it's not null
            HttpRequest?.DeepCopy(out RequestDto);
            targetDto.HttpRequest = RequestDto;
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
