using LPS.Domain;
using LPS.Infrastructure.Monitoring.Hosts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.ResponseService
{
    public interface IResponseProcessingService
    {
        Task<(HttpResponse.SetupCommand command, double dataReceivedSize, TimeSpan streamTime)> ProcessResponseAsync(
            HttpResponseMessage response,
            HttpRequest httpRequest,
            bool cacheResponse,
            string sessionId,
            HostKey hostKey,
            CancellationToken token);
    }
}
