using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.LPSFlow
{
    public partial class Session
    {
        public class Validator : CommandBaseValidator<Session, Session.SetupCommand>
        {
            ILogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            Session _entity;
            SetupCommand _command;

            public Validator(Session entity, SetupCommand command, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _entity = entity;
                _command = command;
                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Session: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }

                #region Validation Rules
                // No validation rules so far
                #endregion

                _command.IsValid = base.Validate();
            }

            public override SetupCommand Command => _command;
            public override Session Entity => _entity;


        }

    }
}
