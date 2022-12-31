using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestValidator : IValidator<LPSTest.SetupCommand, LPSTest>
    {
        public LPSTestValidator(LPSTest.SetupCommand command)
        {
            _command = command;
        }
        LPSTest.SetupCommand _command;
        public LPSTest.SetupCommand Command { get { return _command; } set { value = _command; } }
        public bool Validate(string property)
        {
            switch (property)
            {
                case "-testname":
                    return !(string.IsNullOrEmpty(_command.Name) || !Regex.IsMatch(_command.Name, @"^[\w.-]{2,}$"));
                default:
                    break;
            }
            return true;
        }

    }
}
