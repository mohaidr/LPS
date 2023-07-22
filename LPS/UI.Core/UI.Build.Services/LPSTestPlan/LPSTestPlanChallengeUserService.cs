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
            Console.ForegroundColor= ConsoleColor.Cyan;
                Console.WriteLine("=================== Create Your Test Plan ===================");
            Console.ResetColor();
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

                    int numberOfClients;
                    if (int.TryParse(ChallengeService.Challenge("-numberOfClients"), out numberOfClients))
                    {
                        _command.NumberOfClients = numberOfClients;
                    }

                    continue;
                }

                if (!_validator.Validate("-clientTimeOut"))
                {
                    Console.WriteLine("The number of seconds before the client times out." +
                        "The number should be a valid positive number greater than 0");

                    int clientTimeout;
                    if (int.TryParse(ChallengeService.Challenge("-clientTimeOut"), out clientTimeout))
                    {
                        _command.ClientTimeout = clientTimeout;
                    }

                    continue;
                }

                if (!_validator.Validate("-rampupPeriod"))
                {
                    Console.WriteLine("The time to wait until a new client connects to your site");

                    int rampupPeriod;
                    if (int.TryParse(ChallengeService.Challenge("-rampupPeriod"), out rampupPeriod))
                    {
                        _command.RampUpPeriod = rampupPeriod;
                    }

                    continue;
                }

                if (!_validator.Validate("-maxConnectionsPerServer"))
                {
                    Console.WriteLine("The maximum number of concurrent connections per client per server.\nSetting this property to high value may exhaust your machine resources if the number of clients is high or the targeted server is slow");

                    int maxConnectionsPerServer;
                    if (int.TryParse(ChallengeService.Challenge("-maxConnectionsPerServer"), out maxConnectionsPerServer))
                    {
                        _command.MaxConnectionsPerServer = maxConnectionsPerServer;
                    }

                    continue;
                }

                if (!_validator.Validate("-pooledConnectionLifeTime"))
                {
                    Console.WriteLine("Pooled connection life time defines the maximal connection lifetime in the pool, tracking its age from when the connection was established, regardless of how much time it spent idle or active.\nSee this link for more details https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionlifetime?view=net-8.0");

                    int pooledConnectionLifetime;
                    if (int.TryParse(ChallengeService.Challenge("-pooledConnectionLifeTime"), out pooledConnectionLifetime))
                    {
                        _command.PooledConnectionLifetime = pooledConnectionLifetime;
                    }

                    continue;
                }

                if (!_validator.Validate("-pooledConnectionIdleTimeout"))
                {
                    Console.WriteLine("Pooled connection idle timeout defines the maximum idle time for a connection in the pool.\nSee this link for more details https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionidletimeout?view=net-8.0");

                    int pooledConnectionIdleTimeout;
                    if (int.TryParse(ChallengeService.Challenge("-pooledConnectionIdleTimeout"), out pooledConnectionIdleTimeout))
                    {
                        _command.PooledConnectionIdleTimeout = pooledConnectionIdleTimeout;
                    }

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


                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=================== Add Http Test Case ===================");
                Console.ResetColor();
                var lpsTestCaseCommand = new LPSHttpTestCase.SetupCommand();
                LPSTestCaseValidator validator = new LPSTestCaseValidator(lpsTestCaseCommand);
                LPSTestCaseChallengeUserService lpsTestCaseUserService = new LPSTestCaseChallengeUserService(SkipOptionalFields, lpsTestCaseCommand, validator);
                lpsTestCaseUserService.Challenge();

                Command.LPSTestCases.Add(lpsTestCaseCommand);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=================== Http Test Case Has Been Added ===================");
                Console.ResetColor();

                Console.WriteLine("Enter \"add\" to add new test case to your test plan");

                string action = Console.ReadLine().Trim().ToLower();
                if (action == "add")
                {
                    continue;
                }
                break;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=================== Plan Has Been Created ===================");
            Console.ResetColor();
            _command.IsValid = true;
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _command.ClientTimeout = 0;
                _command.PooledConnectionIdleTimeout = 0;
                _command.PooledConnectionLifetime = 0;
            }
        }
    }
}
