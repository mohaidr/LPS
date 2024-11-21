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
        public HttpRequestDto()
        { 
            Capture = new CaptureDTO();
        }
        public CaptureDTO Capture { get; set; }
        public void DeepCopy(out HttpRequestDto targetDto)
        {
            targetDto = new HttpRequestDto();
            // Call base.Clone() and cast it to HttpRequestDto
            base.Copy(targetDto);
        }
    }
}
