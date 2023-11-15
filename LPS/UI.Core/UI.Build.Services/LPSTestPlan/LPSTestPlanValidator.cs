using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestPlanValidator : IUserValidator<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        LPSTestPlan.SetupCommand _command;
        Dictionary<string, string> _validationErrors;
        public LPSTestPlanValidator(LPSTestPlan.SetupCommand command)
        {
            _command = command;
            _validationErrors = new Dictionary<string, string>();
        }

        public Dictionary<string, string> ValidationErrors => _validationErrors;

        public LPSTestPlan.SetupCommand Command { get { return _command; } }

        public bool Validate(string property)
        {
            bool isValid = true;
            switch (property)
            {
                case "-testname":
                    isValid =!(string.IsNullOrEmpty(_command.Name) || !Regex.IsMatch(_command.Name, @"^[\w.-]{2,}$"));
                    AddValidationMessage(isValid, property, _command.Name);
                    break;
                case "-numberOfClients":
                    isValid= _command.NumberOfClients > 0;
                    AddValidationMessage(isValid, property, _command.NumberOfClients);
                    break;
                case "-rampupPeriod":
                    isValid = _command.RampUpPeriod > 0;
                    AddValidationMessage(isValid, property, _command.RampUpPeriod);
                    break;
                case "-delayClientCreationUntilNeeded":
                    isValid= _command.DelayClientCreationUntilIsNeeded.HasValue;
                    AddValidationMessage(isValid, property, _command.DelayClientCreationUntilIsNeeded);
                    break;
                case "-runInParallel":
                    isValid= _command.RunInParallel.HasValue;
                    AddValidationMessage(isValid, property, _command.RunInParallel);
                    break;
                default:
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
