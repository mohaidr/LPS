using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestCaseValidator : IUserValidator<LPSHttpTestCase.SetupCommand, LPSHttpTestCase>
    {
        public LPSTestCaseValidator(LPSHttpTestCase.SetupCommand command)
        {
            _command = command;
        }
        LPSHttpTestCase.SetupCommand _command;
        public LPSHttpTestCase.SetupCommand Command { get { return _command; } set { } }
        public bool Validate(string property)
        {
            switch (property)
            {
                case "-testCaseName":
                    if (string.IsNullOrEmpty(_command.Name) || !Regex.IsMatch(_command.Name, @"^[\w.-]{2,}$"))
                    {
                        return false;
                    }
                    break;
                case "-iterationMode":
                    if (!_command.Mode.HasValue)
                    {
                        return false;
                    }
                    break;
                case "-requestCount":
                    if ((!_command.RequestCount.HasValue || _command.RequestCount <= 0)
                        || (_command.BatchSize.HasValue && _command.RequestCount.HasValue
                        && _command.BatchSize.Value > _command.RequestCount.Value))
                    {
                        return false;
                    }
                    break;
                case "-coolDownTime":
                    if ((!_command.CoolDownTime.HasValue || _command.CoolDownTime <= 0)
                        || (_command.Duration.HasValue && _command.CoolDownTime.HasValue
                        && _command.CoolDownTime.Value > _command.Duration.Value))
                    {
                        return false;
                    }
                    break;
                case "-batchSize":
                    if ((!_command.BatchSize.HasValue || _command.BatchSize <= 0)
                        || (_command.BatchSize.HasValue && _command.RequestCount.HasValue 
                        && _command.BatchSize.Value> _command.RequestCount.Value))
                    {
                        return false;
                    }
                    break;
                case "-duration":
                    if ((!_command.Duration.HasValue || _command.Duration <= 0)
                        || (_command.Duration.HasValue && _command.CoolDownTime.HasValue
                        && _command.CoolDownTime.Value > _command.Duration.Value))
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

    }
}
