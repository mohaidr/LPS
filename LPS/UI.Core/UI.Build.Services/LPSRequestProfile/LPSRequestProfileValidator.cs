using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestProfileValidator : IUserValidator<LPSHttpRequestProfile.SetupCommand, LPSHttpRequestProfile>
    {
        LPSHttpRequestProfile.SetupCommand _command;
        Dictionary<string, string> _validationErrors;

        public LPSRequestProfileValidator(LPSHttpRequestProfile.SetupCommand command)
        {
            _command = command;
            _validationErrors = new Dictionary<string, string>();
        }
        public Dictionary<string, string> ValidationErrors => _validationErrors;

        public LPSHttpRequestProfile.SetupCommand Command { get { return _command; }}
        public bool Validate(string property)
        {
            string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
            bool isValid = true;
            switch (property)
            {

                case "-httpVersion":
                    if (_command.Httpversion != "1.0" && _command.Httpversion != "1.1")
                    {
                        isValid = false;
                    }
                    AddValidationMessage(isValid, property, _command.Httpversion);
                    break;
                case "-httpMethod":
                    if (_command.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == _command.HttpMethod.ToUpper()))
                    {
                        isValid = false;
                    }
                    AddValidationMessage(isValid, property, _command.HttpMethod);
                    break;
                case "-url":
                    if (!(Uri.TryCreate(_command.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                    {
                        isValid = false;
                    }
                    AddValidationMessage(isValid, property, _command.URL);
                    break;
                case "-downloadHtmlEmbeddedResources":
                    isValid = Command.DownloadHtmlEmbeddedResources.HasValue;
                    AddValidationMessage(isValid, property, _command.DownloadHtmlEmbeddedResources);
                    break;

                case "-saveResponse":
                    isValid = Command.SaveResponse.HasValue;
                    AddValidationMessage(isValid, property, _command.SaveResponse);
                    break;

            }

            return isValid;
        }

        public void ValidateAndThrow(string property)
        {
            if (!Validate(property))
            {
                throw new ArgumentException(_validationErrors[property]);
            }
        }
        public LPSRequestProfileValidator Validate(List<string> properties)
        {
            return this;
        }
        private void AddValidationMessage(bool isValid, string propertyName, object propertyValue)
        {
            if (!isValid)
            {
                _validationErrors.Add(propertyName, $"{propertyValue} is invalid");
            }
            else
            {
                if (_validationErrors.ContainsKey(propertyName))
                {
                    _validationErrors.Remove(propertyName);
                }
            }
        }
    }
}
