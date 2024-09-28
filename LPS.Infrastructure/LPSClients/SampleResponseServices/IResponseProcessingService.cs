using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SampleResponseServices
{
    public interface IResponseProcessingService
    {
        /// <summary>
        /// Creates a response processor for handling response processing logic.
        /// </summary>
        /// <param name="url">The URL of the request.</param>
        /// <param name="saveResponse">Indicates whether the response should be saved.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An instance of IResponseProcessor.</returns>
        Task<IResponseProcessor> CreateResponseProcessorAsync(string url, MimeType responseType, bool saveResponse, CancellationToken token);
    }
}
