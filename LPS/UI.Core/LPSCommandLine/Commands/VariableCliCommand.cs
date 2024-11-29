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
using System.CommandLine;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class VariableCliCommand : ICliCommand
    {
        private readonly Command _rootCliCommand;
        private Command _variableCommand;
        public Command Command => _variableCommand;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal VariableCliCommand(Command rootCliCommand, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _rootCliCommand = rootCliCommand;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            Setup();
        }

        private void Setup()
        {
            _variableCommand = new Command("variable", "Add global variable")
            {
                CommandLineOptions.VariableCommandOptions.ConfigFileArgument
            };
            CommandLineOptions.AddOptionsToCommand(_variableCommand, typeof(CommandLineOptions.VariableCommandOptions));
            _rootCliCommand.AddCommand(_variableCommand);
        }

        public void SetHandler(CancellationToken cancellation)
        {
            _variableCommand.SetHandler((configFile, variable) =>
            {
                try
                {
                    var plandto = ConfigurationService.FetchConfiguration<PlanDto>(configFile);
                    if (plandto != null)
                    {
                        var variableDtoValidator = new VariableValidator();
                        if (variableDtoValidator.Validate(variable).IsValid)
                        {
                            if (plandto.Variables.Any(v => v.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                var selectedVariable = plandto.Variables.FirstOrDefault(v => v.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase));
                                plandto.Variables.Remove(selectedVariable);

                            }
                            plandto.Variables.Add(variable);
                        }
                        else
                        {
                            variableDtoValidator.ValidateAndThrow(variable);
                        }
                    }
                    else
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId, "No plan defined for adding the variable to.", LPSLoggingLevel.Error);
                    }
                    ConfigurationService.SaveConfiguration(configFile, plandto);
                }
                catch(Exception ex) {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"{ex.Message}\r\n{ex.InnerException?.Message}\r\n{ex.StackTrace}", LPSLoggingLevel.Error);

                }
            },
            CommandLineOptions.VariableCommandOptions.ConfigFileArgument,
            new VariableBinder());
        }
    }
}
