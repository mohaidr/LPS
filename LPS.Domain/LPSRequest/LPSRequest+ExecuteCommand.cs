using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSRequest
    {

        public class ExecuteCommand : IAsyncCommand<LPSRequest>
        {
            public ExecuteCommand()
            {
            }

            public async Task ExecuteAsync(LPSRequest entity, CancellationToken cancellationToken)
            { 
                await entity.ExecuteAsync(this, cancellationToken);
            }
        }

        protected async Task ExecuteAsync(ExecuteCommand command, CancellationToken cancellationToken)
        {
            await _logger.LogAsync("", "LPSRequest Default Implementation Was Called ", LPSLoggingLevel.Warning);
        }
    }
}
