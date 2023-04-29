using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestCaseValidator : IValidator<LPSHttpTestCase.SetupCommand, LPSHttpTestCase>
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
