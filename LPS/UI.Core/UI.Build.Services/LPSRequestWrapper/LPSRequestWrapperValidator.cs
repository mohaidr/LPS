using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestWrapperValidator : IValidator<LPSRequestWrapper.SetupCommand, LPSRequestWrapper>
    {
        public LPSRequestWrapperValidator(LPSRequestWrapper.SetupCommand command)
        {
            _command = command;
        }
        LPSRequestWrapper.SetupCommand _command;
        public LPSRequestWrapper.SetupCommand Command { get { return _command; } set { } }
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
                case "-repeat":

                    if (_command.NumberofAsyncRepeats <= 0)
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

    }
}
