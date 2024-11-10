using FluentValidation;
using FluentValidation.Results;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.DTOs;
using LPS.Infrastructure.Common;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Services;
using Spectre.Console;
using System;
using System.CommandLine;

using ValidationResult = FluentValidation.Results.ValidationResult;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class IterationCliCommand : ICliCommand
    {
        private readonly Command _rootCliCommand;
        private Command _iterationCommand;
        public Command Command => _iterationCommand;
        private RefCliCommand _refCliCommand;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal IterationCliCommand(Command rootCliCommand, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            _rootCliCommand = rootCliCommand;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            Setup();
        }
        private void Setup()
        {
            _iterationCommand = new Command("iteration", "Add an http iteration")
            {
                CommandLineOptions.LPSIterationCommandOptions.ConfigFileArgument // Add ConfigFileArgument here
            };
            CommandLineOptions.AddOptionsToCommand(_iterationCommand, typeof(CommandLineOptions.LPSIterationCommandOptions));
            _refCliCommand = new RefCliCommand(_iterationCommand, _logger, _runtimeOperationIdProvider);
            _rootCliCommand.AddCommand(_iterationCommand);
        }
        public void SetHandler(CancellationToken cancellationToken)
        {
            _iterationCommand.SetHandler((configFile, roundName, iteration, isGlobal) =>
            {
                var iterationValidator = new IterationValidator(iteration);
                ValidationResult results = iterationValidator.Validate();

                if (results.IsValid)
                {
                    try
                    {
                        var planDto = ConfigurationService.FetchConfiguration<PlanDto>(configFile);
                        bool isRoundNameEmpty = string.IsNullOrEmpty(roundName);
                        // Determine where to add the iteration based on the global and roundName options
                        if (isGlobal || isRoundNameEmpty)
                        {
                            var existingGlobalIteration = planDto?.Iterations.FirstOrDefault(i => i.Name == iteration.Name);
                            if (existingGlobalIteration != null)
                            {
                                planDto?.Iterations.Remove(existingGlobalIteration);
                            }
                            // Add iteration to global iterations
                            planDto?.Iterations.Add(iteration);
                        }

                        if (!isRoundNameEmpty)
                        {
                            var round = planDto?.Rounds.FirstOrDefault(r => r.Name == roundName);
                            if (round != null)
                            {
                                if (isGlobal)
                                {
                                    // Add as a global iteration but also create a setup command in the round with the same name
                                    round.ReferencedIterations.Add(new ReferenceIterationDto { Name = iteration.Name });
                                }
                                else
                                {
                                    var existingRoundIteration = round.Iterations.FirstOrDefault(i => i.Name == iteration.Name);
                                    if (existingRoundIteration != null)
                                    {
                                        round.Iterations.Remove(existingRoundIteration);
                                    }
                                    // Add iteration as a local iteration within the round
                                    round.Iterations.Add(iteration);
                                }
                            }
                            else
                            {
                                throw new ArgumentException($"Invalid Round Name {roundName}");
                            }
                        }
                        ConfigurationService.SaveConfiguration(configFile, planDto);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId, $"{ex.Message}\r\n{ex.InnerException?.Message}\r\n{ex.StackTrace}", LPSLoggingLevel.Error);
                    }
                }
                else
                {
                    results.PrintValidationErrors();
                }

            },
            CommandLineOptions.LPSIterationCommandOptions.ConfigFileArgument,
            CommandLineOptions.LPSIterationCommandOptions.RoundNameOption,
            new IterationCommandBinder(),
            CommandLineOptions.LPSIterationCommandOptions.GlobalOption);

            _refCliCommand.SetHandler(cancellationToken);
        }
    }
}
