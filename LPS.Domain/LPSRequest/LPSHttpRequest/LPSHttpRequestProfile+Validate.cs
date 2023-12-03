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

    public partial class LPSHttpRequestProfile
    {
        public new class Validator: IDomainValidator<LPSHttpRequestProfile, LPSHttpRequestProfile.SetupCommand>
        {
            ILPSLogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSHttpRequestProfile entity, LPSHttpRequestProfile.SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider) 
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSHttpRequestProfile entity, SetupCommand command)
            {
                if (command == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid Entity Command", LPSLoggingLevel.Warning);
                    throw new ArgumentNullException(nameof(command));
                }

                command.IsValid = true;
                string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

                if (command.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == command.HttpMethod.ToUpper()))
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid Http Method", LPSLoggingLevel.Warning);

                    command.IsValid = false;
                }

                if (command.Httpversion != "1.0" && command.Httpversion != "1.1" && command.Httpversion != "2.0")
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "invalid http version, 1.0, 1.1 and 2.0 are the supported http versions", LPSLoggingLevel.Warning);
                    command.IsValid = false;
                }
                
                if (!(Uri.TryCreate(command.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid URL", LPSLoggingLevel.Warning);
                    command.IsValid = false;
                }

                if (!command.DownloadHtmlEmbeddedResources.HasValue)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "DownloadHtmlEmbeddedResources must be (Y/N)", LPSLoggingLevel.Warning);
                    command.IsValid = false;
                }

                if (!command.SaveResponse.HasValue)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "SaveResponse must be (Y/N)", LPSLoggingLevel.Warning);
                    command.IsValid = false;
                }

                //TODO: Validate http headers
            }
        }
    }
}
