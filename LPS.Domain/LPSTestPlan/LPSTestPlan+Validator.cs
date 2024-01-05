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

                if (entity == null && command.Id.HasValue)
                {
                    throw new InvalidOperationException("Invalid Entity State");
                }

                if (entity.Id != default && /*command.Id.HasValue &&*/ entity.Id != command.Id)
                {
                  //  command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Run: Entity Id Can't be Changed", LPSLoggingLevel.Error);
                  //  throw new InvalidOperationException("LPS Run: Entity Id Can't be Changed");
                }

                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Plan: Entity Id Can't be Changed", LPSLoggingLevel.Error);
                    throw new InvalidOperationException("LPS Plan: Entity Id Can't be Changed");
                }

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

                if (command.LPSHttpRuns != null || command.LPSHttpRuns.Count>0)
                {
                    foreach (var lpsRunCommand in command.LPSHttpRuns)
                    {
                        if (!lpsRunCommand.Id.HasValue)
                        {
                            new LPSHttpRun.Validator(null, lpsRunCommand, _logger, _runtimeOperationIdProvider);
                            if (!lpsRunCommand.IsValid)
                            {
                                _logger.Log(_runtimeOperationIdProvider.OperationId, $"The lpsRun assosiated with the request {lpsRunCommand.Name} has an invalid input, please review the above errors and fix them", LPSLoggingLevel.Warning);
                                command.IsValid = false;
                            }
                        }
                        else
                        {
                            var lpsRunEntity = entity.LPSHttpRuns.Single(entity => entity.Id == lpsRunCommand.Id);
                            if (lpsRunEntity == null)
                            {
                                _logger.Log(_runtimeOperationIdProvider.OperationId, $"The lpsRun does not exist", LPSLoggingLevel.Warning);
                                command.IsValid = false;
                            }
                            else
                            {
                                lpsRunCommand.Execute(lpsRunEntity);
                                if (!lpsRunCommand.IsValid)
                                {
                                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"The lpsRun assosiated with the request {lpsRunCommand.Name} has an invalid input, please review the above errors and fix them", LPSLoggingLevel.Warning);
                                    command.IsValid = false;
                                }
                            }
                        }
                    }
                }
                else
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid plan, the plan should at least contain one http request", LPSLoggingLevel.Warning);
                }
            }
        }
    }
}

