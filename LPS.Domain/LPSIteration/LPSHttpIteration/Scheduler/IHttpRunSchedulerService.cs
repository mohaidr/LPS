using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.LPSHttpRun.Scheduler
{
    public interface IHttpRunSchedulerService
    {
        Task ScheduleHttpRunExecutionAsync(DateTime scheduledTime, HttpIteration httpRun, IClientService<HttpSession, HttpResponse> httpClient);
    }
}
