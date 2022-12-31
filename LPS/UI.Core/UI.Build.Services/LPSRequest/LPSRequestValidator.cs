using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestValidator : IValidator<LPSRequest.SetupCommand, LPSRequest>
    {
        public LPSRequestValidator(LPSRequest.SetupCommand command)
        {
            _command = command;
        }
        LPSRequest.SetupCommand _command;
        public LPSRequest.SetupCommand Command { get { return _command; } set { value = _command; } }
        public bool Validate(string property)
        {
            string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

            switch (property)
            {
            
                case "-httpversion":
                    if (_command.Httpversion != "1.0" && _command.Httpversion != "1.1")
                    {
                        return false;
                    }
                    break;
                case "-httpmethod":
                    if (_command.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == _command.HttpMethod.ToUpper()))
                    {
                        return false;
                    }
                    break;
                case "-timeout":
                    if (_command.TimeOut <= 0)
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
            }
            return true;
        }

    }
}
