using FluentValidation;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.LPSFlow.LPSHandlers
{
    public partial class CaptureHandler : ISessionHandler
    {
        public class Validator : CommandBaseValidator<CaptureHandler, CaptureHandler.SetupCommand>
        {
            ILogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            CaptureHandler _entity;
            SetupCommand _command;

            public Validator(CaptureHandler entity, SetupCommand command, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _entity = entity;
                _command = command;
                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS CapturHandler: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }

                #region Validation Rules
                RuleFor(command => command.Variable)
                    .NotNull()
                    .NotEmpty();
                RuleFor(command => command.As)
                    .Must(@as =>
                    {
                        @as ??= string.Empty;
                        return @as.Equals("JSON", StringComparison.OrdinalIgnoreCase)
                        || @as.Equals("XML", StringComparison.OrdinalIgnoreCase) 
                        || @as.Equals("Regex", StringComparison.OrdinalIgnoreCase) 
                        || string.IsNullOrEmpty(@as);
                    });
                #endregion
                _command.IsValid = base.Validate();
            }

            public override SetupCommand Command => _command;
            public override CaptureHandler Entity => _entity;


        }

    }
}
