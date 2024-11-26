using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.DTOs
{
    public class HttpRequestDto : HttpRequest.SetupCommand
    {
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public HttpRequestDto()
        {             
        }
        public CaptureHandlerDto Capture { get; set; }
        public void DeepCopy(out HttpRequestDto targetDto)
        {
            targetDto = new HttpRequestDto();
            // Call base.Clone() and cast it to HttpRequestDto
            base.Copy(targetDto);
        }
    }
}
