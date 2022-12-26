using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestWrapperValidator : IValidator<LPSRequestWrapper.SetupCommand, LPSRequestWrapper>
    {
        public bool Validate(string property, LPSRequestWrapper.SetupCommand setupCommand)
        {
            string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

            switch (property)
            {
                case "-requestname":
                    if (string.IsNullOrEmpty(setupCommand.Name) || !Regex.IsMatch(setupCommand.Name, @"^[\w.-]{2,}$"))
                    {
                        return false;
                    }
                    break;
                case "-httpversion":
                    if (setupCommand.LPSRequest.Httpversion != "1.0" && setupCommand.LPSRequest.Httpversion != "1.1")
                    {
                        return false;
                    }
                    break;
                case "-httpmethod":
                    if (setupCommand.LPSRequest.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == setupCommand.LPSRequest.HttpMethod.ToUpper()))
                    {
                        return false;
                    }
                    break;
                case "-timeout":
                    if (setupCommand.LPSRequest.TimeOut <= 0)
                    {
                        return false;
                    }
                    break;
                case "-repeat":

                    if (setupCommand.NumberofAsyncRepeats <= 0)
                    {
                        return false;
                    }
                    break;
                case "-url":
                    if (!(Uri.TryCreate(setupCommand.LPSRequest.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

    }
}
