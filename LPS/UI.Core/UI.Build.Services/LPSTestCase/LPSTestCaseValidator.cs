using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestCaseValidator : IValidator<LPSTestCase.SetupCommand, LPSTestCase>
    {
        public LPSTestCaseValidator(LPSTestCase.SetupCommand command)
        {
            _command = command;
        }
        LPSTestCase.SetupCommand _command;
        public LPSTestCase.SetupCommand Command { get { return _command; } set { } }
        public bool Validate(string property)
        {
            switch (property)
            {
                case "-requestname":
                    if (string.IsNullOrEmpty(_command.Name) || !Regex.IsMatch(_command.Name, @"^[\w.-]{2,}$"))
                    {
                        return false;
                    }
                    break;
                case "-requestCount":
                    if (!_command.RequestCount.HasValue || _command.RequestCount <= 0)
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

    }
}
