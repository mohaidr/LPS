using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestPlanValidator : IValidator<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        public LPSTestPlanValidator(LPSTestPlan.SetupCommand command)
        {
            _command = command;
        }
        LPSTestPlan.SetupCommand _command;
        public LPSTestPlan.SetupCommand Command { get { return _command; } set { value = _command; } }
        public bool Validate(string property)
        {
            switch (property)
            {
                case "-testname":
                    return !(string.IsNullOrEmpty(_command.Name) || !Regex.IsMatch(_command.Name, @"^[\w.-]{2,}$"));
                case "-numberOfClients":
                    return _command.NumberOfClients > 0;
                case "-clientTimeOut":
                    return _command.ClientTimeout > 0;
                case "-rampupPeriod":
                    return _command.RampUpPeriod > 0;
                case "-maxConnectionsPerServer":
                    return _command.MaxConnectionsPerServer > 0;
                case "-pooledConnectionLifetime":
                    return _command.PooledConnectionLifetime > 0;
                case "-pooledConnectionIdleTimeout":
                    return _command.PooledConnectionIdleTimeout > 0;
                case "-delayClientCreationUntilNeeded":
                    return _command.DelayClientCreationUntilIsNeeded.HasValue;
                default:
                    break;
            }
            return true;
        }

    }
}
