using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Enums
{
    public enum IterationMode
    {
        /* 
        * D refers to Duration
        * C refers to Cool Down
        * B refers to Batch Size  
        * R refers to Request Count
        */
        DCB = 0,
        CRB = 1,
        CB = 2,
        R = 3,
        D = 4
    }
}
