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
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Validation;

namespace LPS.Domain
{

    public partial class LPSResponse
    {
        public class Validator : CommandBaseValidator<LPSResponse, LPSResponse.SetupCommand>
        {
            ILPSLogger _logger;
            ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
            LPSResponse _entity;
            LPSResponse.SetupCommand _command;
            public Validator(LPSResponse entity, SetupCommand command, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
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
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Response: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }

                command.IsValid = base.Validate();
            }

            public override LPSResponse.SetupCommand Command => _command;
            public override LPSResponse Entity => _entity;
        }
    }
}
