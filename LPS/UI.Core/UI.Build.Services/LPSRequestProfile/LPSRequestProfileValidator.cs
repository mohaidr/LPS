using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestProfileValidator : IUserValidator<LPSHttpRequestProfile.SetupCommand, LPSHttpRequestProfile>
    {
        public LPSRequestProfileValidator(LPSHttpRequestProfile.SetupCommand command)
        {
            _command = command;
        }
        LPSHttpRequestProfile.SetupCommand _command;
        public LPSHttpRequestProfile.SetupCommand Command { get { return _command; } set { value = _command; } }
        public bool Validate(string property)
        {
            string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

            switch (property)
            {
            
                case "-httpVersion":
                    if (_command.Httpversion != "1.0" && _command.Httpversion != "1.1")
                    {
                        return false;
                    }
                    break;
                case "-httpMethod":
                    if (_command.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == _command.HttpMethod.ToUpper()))
                    {
                        return false;
                    }
                    break;
                case "-url":
                    if (!(Uri.TryCreate(_command.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                    {
                        return false;
                    }
                    break;
                case "-downloadHtmlEmbeddedResources":
                    return Command.DownloadHtmlEmbeddedResources.HasValue;
                case "-saveResponse":
                    return Command.SaveResponse.HasValue;
            }
            return true;
        }

    }
}
