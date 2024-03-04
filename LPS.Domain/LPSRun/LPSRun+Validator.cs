using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Validation;
using System;
using System.Text.RegularExpressions;

namespace LPS.Domain
{

    public partial class LPSRun
    {
   
        public class Validator: CommandBaseValidator<LPSRun, LPSRun.SetupCommand>
        {
            ILPSLogger _logger;
            ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
            LPSRun _entity;
            SetupCommand _command;
            public Validator(LPSRun entity, SetupCommand command, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _entity = entity;
                _command = command;

                #region Validation Rules
                    // No validation rules so far
                #endregion

                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Run: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }
                _command.IsValid = true;
            }

            public override SetupCommand Command => _command;

            public override LPSRun Entity => _entity;

        }
    }
}

