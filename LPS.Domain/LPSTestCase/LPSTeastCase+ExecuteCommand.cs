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

    public partial class LPSTestCase
    {
        public class ExecuteCommand : IAsyncCommand<LPSTestCase>
        {

            public ExecuteCommand()
            {
            }

            async public Task ExecuteAsync(LPSTestCase entity, CancellationToken cancellationToken)
            {
                await entity.ExecuteAsync(this, cancellationToken);
            }
        }
        async private Task ExecuteAsync(ExecuteCommand command, CancellationToken cancellationToken)
        {
            if (this.IsValid)
            {
                await _logger.LogAsync("0000-0000-0000", "LPSTestCase Default Implementation Was Called ", LPSLoggingLevel.Warning);

            }
        }
    }
}
