using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.DTOs
{
    public class HttpRequestProfileDto : HttpRequestProfile.SetupCommand
    {
        public void DeepCopy(out HttpRequestProfileDto targetDto)
        {
            targetDto = new HttpRequestProfileDto();
            // Call base.Clone() and cast it to HttpRequestProfileDto
            base.Copy(targetDto);
        }
    }
}
