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

    public partial class LPSHttpRequest
    {
        public new class Validator: IDomainValidator<LPSHttpRequest, LPSHttpRequest.SetupCommand>
        {
            ILPSLogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSHttpRequest entity, LPSHttpRequest.SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider) 
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public async void Validate(LPSHttpRequest entity, SetupCommand command)
            {
                if (command == null)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Invalid Entity Command", LPSLoggingLevel.Warning);
                    throw new ArgumentNullException(nameof(command));
                }

                command.IsValid = true;
                string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

                if (command.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == command.HttpMethod.ToUpper()))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Invalid Http Method", LPSLoggingLevel.Warning);

                    command.IsValid = false;
                }

                if (command.Httpversion != "1.0" && command.Httpversion != "1.1")
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "invalid http version, 1.0 and 1.1 are supported versions", LPSLoggingLevel.Warning);
                    command.IsValid = false;
                }
                if (!(Uri.TryCreate(command.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Invalid URL", LPSLoggingLevel.Warning);
                    command.IsValid = false;
                }

                //TODO: Validate http headers
            }
        }
    }
}
