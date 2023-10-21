using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
   
        public class Validator: IDomainValidator<LPSTestPlan, LPSTestPlan.SetupCommand>
        {

            ILPSLogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSTestPlan entity, SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSTestPlan entity,SetupCommand command)
            {
                command.IsValid = true;
                if (string.IsNullOrEmpty(command.Name)  || !Regex.IsMatch(command.Name, @"^[\w.-]{2,}$"))
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid Test Name, The name should at least be of 2 charachters and can only contain letters, numbers, ., _ and -", LPSLoggingLevel.Warning);
                }
                if (command.NumberOfClients < 1)
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Number of clients can't be less than 1, at least one user has to be created.", LPSLoggingLevel.Warning);
                }

                if (!command.DelayClientCreationUntilIsNeeded.HasValue) 
                { 
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Delay client creation until needed must have a value.", LPSLoggingLevel.Warning);

                }

                if (!command.RunInParallel.HasValue)
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Run in parallel must have a value.", LPSLoggingLevel.Warning);

                }

                if (command.LPSTestCases != null || command.LPSTestCases.Count>0)
                {
                    foreach (var lpsTestCaseCommand in command.LPSTestCases)
                    {
                        new LPSHttpTestCase.Validator(null, lpsTestCaseCommand, _logger, _runtimeOperationIdProvider);
                        if (!lpsTestCaseCommand.IsValid)
                        {
                            _logger.Log(_runtimeOperationIdProvider.OperationId, $"The http request named {lpsTestCaseCommand.Name} has an invalid input, please review the above errors and fix them", LPSLoggingLevel.Warning);
                            command.IsValid = false;
                        }
                    }
                }
                else
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid async test, the test should at least contain one http request", LPSLoggingLevel.Warning);
                }
            }
        }
    }
}

