using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.DTOs
{
    public class HttpSessionDto : HttpSession.SetupCommand
    {
        public void DeepCopy(out HttpSessionDto targetDto)
        {
            targetDto = new HttpSessionDto();
            // Call base.Clone() and cast it to HttpSessionDto
            base.Copy(targetDto);
        }
    }
}
