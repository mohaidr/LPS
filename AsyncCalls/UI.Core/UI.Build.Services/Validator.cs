using AsyncCalls.UI.Common;
using AsyncTest.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AsyncCalls.UI.Core.UI.Build.Services
{
    internal class HttpAsyncRequestWrapperValidator : IValidator<HttpAsyncRequestWrapper.SetupCommand, HttpAsyncRequestWrapper>
    {
        public bool Validate(string property, HttpAsyncRequestWrapper.SetupCommand setupCommand)
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
                    if (setupCommand.HttpRequest.Httpversion != "1.0" && setupCommand.HttpRequest.Httpversion != "1.1")
                    {
                        return false;
                    }
                    break;
                case "-httpmethod":
                    if (setupCommand.HttpRequest.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == setupCommand.HttpRequest.HttpMethod.ToUpper()))
                    {
                        return false;
                    }
                    break;
                case "-timeout":
                    if (setupCommand.HttpRequest.TimeOut <= 0)
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
                    if (!(Uri.TryCreate(setupCommand.HttpRequest.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

    }
}
