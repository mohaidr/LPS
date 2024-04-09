using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface ILPSMonitoringEnroller
    {
        public void Enroll(LPSHttpRun lpsHttpRun, ILPSLogger logger = default, ILPSRuntimeOperationIdProvider lpsRuntimeOperationIdProvider = default);
        public void Withdraw(LPSHttpRun lpsHttpRun, ILPSLogger logger = default, ILPSRuntimeOperationIdProvider lpsRuntimeOperationIdProvider = default);
    }
}
