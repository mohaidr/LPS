using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using LPS.Domain;
using LPS.UI.Common;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestPlanChallengeUserService : IChallengeUserService<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        IValidator<LPSTestPlan.SetupCommand, LPSTestPlan> _validator;
        LPSTestPlan.SetupCommand _command;
        public LPSTestPlan.SetupCommand Command { get { return _command; } set { value = _command; } }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }

        private bool _skipOptionalFields;
        public LPSTestPlanChallengeUserService(bool skipOptionalFields, LPSTestPlan.SetupCommand command, IValidator<LPSTestPlan.SetupCommand, LPSTestPlan> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }


        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }
            while (true)
            {
                if (!_validator.Validate("-testname"))
                {
                    Console.WriteLine("Test name should at least be of 2 charachters and can only contains letters, numbers, ., _ and -");
                    _command.Name = ChallengeService.Challenge("-testName");
                    continue;
                }

                if (!_validator.Validate("-numberOfClients"))
                {
                    Console.WriteLine("The number of clients to connect to your site. " +
                        "The number should be a valid positive number greater than 0");
                    try
                    {
                        _command.NumberOfClients = int.Parse(ChallengeService.Challenge("-numberOfClients"));
                    }
                    catch { }
                    continue;
                }

                if (!_validator.Validate("-clientTimeOut"))
                {
                    Console.WriteLine("The number of seconds before the client times out." +
                        "The number should be a valid positive number greater than 0");
                    try
                    {
                        _command.ClientTimeout = int.Parse(ChallengeService.Challenge("-clientTimeOut"));
                    }
                    catch { }
                    continue;
                }

                if (!_validator.Validate("-rampupPeriod"))
                {
                    Console.WriteLine("The time to wait until a new client connects to your site");
                    try
                    {
                        _command.RampUpPeriod = int.Parse(ChallengeService.Challenge("-rampupPeriod"));
                    }
                    catch { }
                    continue;
                }

                if (!_validator.Validate("-maxConnectionsPerServer"))
                {
                    Console.WriteLine("The maximum number of concurrent connections per server");
                    try
                    {
                        _command.MaxConnectionsPerServer = int.Parse(ChallengeService.Challenge("-maxConnectionsPerServer"));
                    }
                    catch { }
                    continue;
                }

                if (!_validator.Validate("-pooledConnectionLifetime"))
                {
                    Console.WriteLine("Defines the maximal connection lifetime in the pool, tracking its age from when the connection was established, regardless of how much time it spent idle or active. See this link for more details https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionlifetime?view=net-8.0");
                    try
                    {
                        _command.PooledConnectionLifetime = int.Parse(ChallengeService.Challenge("-pooledConnectionLifetime"));
                    }
                    catch { }
                    continue;
                }

                if (!_validator.Validate("-pooledConnectionIdleTimeout"))
                {
                    Console.WriteLine("Defined the maximum idle time for a connection in the pool.See this link for more details https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionidletimeout?view=net-8.0");
                    try
                    {
                        _command.PooledConnectionIdleTimeout = int.Parse(ChallengeService.Challenge("-pooledConnectionIdleTimeout"));
                    }
                    catch { }
                    continue;
                }

                if (!_validator.Validate("-delayClientCreationUntilNeeded"))
                {
                    Console.WriteLine("Delay the client creation until the client is needed");

                    switch (ChallengeService.Challenge("-delayClientCreationUntilNeeded").ToUpper())
                    {
                        case "Y":
                            _command.DelayClientCreationUntilIsNeeded = true; break;
                        case "N":
                            _command.DelayClientCreationUntilIsNeeded = false; break;
                    }
                    continue;
                }

                var lpsTestCaseCommand = new LPSHttpTestCase.SetupCommand();
                LPSTestCaseValidator validator = new LPSTestCaseValidator(lpsTestCaseCommand);
                LPSTestCaseChallengeUserService lpsTestCaseUserService = new LPSTestCaseChallengeUserService(SkipOptionalFields, lpsTestCaseCommand, validator);
                lpsTestCaseUserService.Challenge();

                Command.LPSTestCases.Add(lpsTestCaseCommand);

                Console.WriteLine("Enter \"add\" to add new test case to your test plan");

                string action = Console.ReadLine().Trim().ToLower();
                if (action == "add")
                {
                    continue;
                }
                break;
            }
            _command.IsValid = true;
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _command.NumberOfClients = 0;
            }
        }
    }
}
