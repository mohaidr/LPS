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

    public partial class LPSHttpTestCase
    {

        new public class Validator : IDomainValidator<LPSHttpTestCase, LPSHttpTestCase.SetupCommand>
        {

            ILPSLogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSHttpTestCase entity, SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                Validate(entity, command);
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
            }

            public async void Validate(LPSHttpTestCase entity, SetupCommand command)
            {
                command.IsValid = true;
                Console.ForegroundColor = ConsoleColor.Yellow;

                if (string.IsNullOrEmpty(command.Name) || !Regex.IsMatch(command.Name, @"^[\w.-]{2,}$"))
                {

                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Please Provide a Valid Name, The Name Should At least Be of 2 Charachters And Can Only Contains Letters, Numbers, ., _ and -", LPSLoggingLevel.Warning);

                    command.IsValid = false;
                }

                if (command.Mode.HasValue)
                {
                    bool invalidIterationModeCombination = false;
                    if (command.Mode.Value == IterationMode.DCB)
                    {
                        if (!command.Duration.HasValue || command.Duration.Value <= 0
                            || !command.CoolDownTime.HasValue || command.CoolDownTime.Value <= 0
                            || !command.BatchSize.HasValue || command.BatchSize.Value <= 0
                            || command.RequestCount.HasValue)
                        {
                            command.IsValid = false;
                            invalidIterationModeCombination = true;
                        }
                    }
                    else if (command.Mode.Value == IterationMode.CRB)
                    {
                        if (!command.CoolDownTime.HasValue || command.CoolDownTime.Value <= 0
                            || !command.RequestCount.HasValue || command.RequestCount.Value <= 0
                            || !command.BatchSize.HasValue || command.BatchSize.Value <= 0
                            || command.Duration.HasValue)
                        {
                            command.IsValid = false;
                            invalidIterationModeCombination = true;

                        }
                    }
                    else if (command.Mode.Value == IterationMode.CB)
                    {
                        if (!command.CoolDownTime.HasValue || command.CoolDownTime.Value <= 0
                            || !command.BatchSize.HasValue || command.BatchSize.Value <= 0
                            || command.Duration.HasValue
                            || command.RequestCount.HasValue)
                        {
                            command.IsValid = false;
                            invalidIterationModeCombination = true;

                        }
                    }
                    else if (command.Mode.Value == IterationMode.R)
                    {
                        if (!command.RequestCount.HasValue || command.RequestCount.Value <= 0
                            || command.Duration.HasValue
                            || command.BatchSize.HasValue
                            || command.CoolDownTime.HasValue)
                        {
                            command.IsValid = false;
                            invalidIterationModeCombination = true;

                        }

                    }
                    else if (command.Mode.Value == IterationMode.D)
                    {
                        if (!command.Duration.HasValue || command.Duration.Value <= 0
                            || command.RequestCount.HasValue
                            || command.BatchSize.HasValue
                            || command.CoolDownTime.HasValue)
                        {
                            command.IsValid = false;
                            invalidIterationModeCombination = true;


                        }
                    }
                    if (invalidIterationModeCombination == true)
                    {

                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Invalid combination, you have to use one of the below combinations" +
                            "\t- Duration && Cool Down Time && Batch Size" +
                            "\t- Cool Down Time && Number Of Requests && Batch Size" +
                            "\t- Cool Down Time && Batch Size. Requests will not stop until you stop it" +
                            "\t- Number Of Requests. Test will complete when all the requests are completed" +
                            "\t- Duration. Test will complete once the duration expires", LPSLoggingLevel.Warning);
                    }
                }
                else
                {
                    command.IsValid = false;
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Iteration mode can't be null", LPSLoggingLevel.Warning);
                }

                if (command.Duration.HasValue && command.CoolDownTime.HasValue && command.CoolDownTime.Value > command.Duration.Value)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Cool Down Time can't be larger than the Duration", LPSLoggingLevel.Warning);

                    command.IsValid = false;
                }

                if (command.RequestCount.HasValue && command.BatchSize.HasValue && command.BatchSize.Value > command.RequestCount.Value)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Batch Size can't be larger than the request count", LPSLoggingLevel.Warning);
                    command.IsValid = false;
                }

                if (command.LPSRequest != null)
                {
                    new LPSHttpRequest.Validator(null, command.LPSRequest, _logger, _runtimeOperationIdProvider);
                    if (!command.LPSRequest.IsValid)
                    {
                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Invalid Http Request", LPSLoggingLevel.Warning);

                        command.IsValid = false;
                    }
                }
                Console.ResetColor();

            }
        }
    }
}

