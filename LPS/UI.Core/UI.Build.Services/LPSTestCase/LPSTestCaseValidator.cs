using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestCaseValidator : IUserValidator<LPSHttpTestCase.SetupCommand, LPSHttpTestCase>
    {
        LPSHttpTestCase.SetupCommand _command;
        Dictionary<string, string> _validationErrors;

        public LPSTestCaseValidator(LPSHttpTestCase.SetupCommand command)
        {
            _command = command;
            _validationErrors = new Dictionary<string, string>();
        }
        public Dictionary<string, string> ValidationErrors => _validationErrors;

        public LPSHttpTestCase.SetupCommand Command { get { return _command; } }
        public bool Validate(string property)
        {
            bool isValid = true;
            switch (property)
            {
                case "-testCaseName":
                    isValid = !(string.IsNullOrEmpty(_command.Name) || !Regex.IsMatch(_command.Name, @"^[\w.-]{2,}$"));
                    AddValidationMessage(isValid, property, _command.Name);
                    break;
                case "-iterationMode":
                    isValid = _command.Mode.HasValue;
                    break;
                case "-requestCount":
                    isValid = !((!_command.RequestCount.HasValue || _command.RequestCount <= 0)
                    || (_command.BatchSize.HasValue && _command.RequestCount.HasValue
                    && _command.BatchSize.Value > _command.RequestCount.Value));
                    AddValidationMessage(isValid, property, _command.RequestCount);
                    break;
                case "-coolDownTime":
                    isValid = !((!_command.CoolDownTime.HasValue || _command.CoolDownTime <= 0)
                    || (_command.Duration.HasValue && _command.CoolDownTime.HasValue
                    && _command.CoolDownTime.Value > _command.Duration.Value));
                    AddValidationMessage(isValid, property, _command.CoolDownTime);
                    break;
                case "-batchSize":
                    isValid = !((!_command.BatchSize.HasValue || _command.BatchSize <= 0)
                         || (_command.BatchSize.HasValue && _command.RequestCount.HasValue
                         && _command.BatchSize.Value > _command.RequestCount.Value));
                        AddValidationMessage(isValid, property, _command.BatchSize);
                    break;
                case "-duration":
                    isValid = !((!_command.Duration.HasValue || _command.Duration <= 0)
                    || (_command.Duration.HasValue && _command.CoolDownTime.HasValue
                    && _command.CoolDownTime.Value > _command.Duration.Value));
                    AddValidationMessage(isValid, property, _command.Duration);
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

        private void AddValidationMessage(bool isValid, string propertyName, object propertyValue)
        {
            if (!isValid)
            {
                _validationErrors.Add(propertyName, $"{propertyValue} is invalid");
            }
        }
    }
}
