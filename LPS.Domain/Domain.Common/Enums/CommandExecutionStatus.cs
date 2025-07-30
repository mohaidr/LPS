using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Enums
{
    //it is important to give the enums number based on their strength as this will be used to calculate the aggregate status
    // this design may change
    public enum CommandExecutionStatus
    {
        Scheduled = 0,
        Ongoing = 1,
        Completed = 2,
        Failed = 3,
        Terminated = 4,
        Cancelled = 5,
        Unkown = -1,
    }

    public enum EntityExecutionStatus
    {
        NotStarted = 0,
        Scheduled = 1,
        Ongoing = 2,
        Success = 3,
        Failed = 4,
        Terminated = 5,
        Cancelled = 6,
        Unkown = -1,
    }
}
