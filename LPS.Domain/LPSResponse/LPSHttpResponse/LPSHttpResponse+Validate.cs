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

namespace LPS.Domain
{

    public partial class LPSHttpResponse
    {
        public new class Validator: IDomainValidator<LPSHttpResponse, LPSHttpResponse.SetupCommand>
        {
            ILPSLogger _logger;
            ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSHttpResponse entity, LPSHttpResponse.SetupCommand command, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider) 
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSHttpResponse entity, SetupCommand command)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid Entity", LPSLoggingLevel.Warning);
                    throw new ArgumentNullException(nameof(entity));
                }

                if (command == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid Entity Command", LPSLoggingLevel.Warning);
                    throw new ArgumentNullException(nameof(command));
                }
                command.IsValid = true;
                //as of now only checking if a file does exist, the location might be anywhere online so will need to update this logic
                if (!string.IsNullOrEmpty(command.LocationToResponse) && command.LocationToResponse.ToCharArray().Any(c => Path.GetInvalidPathChars().Contains(c)))
                {
                    Console.WriteLine("Path contains illegal charcter");
                    command.IsValid= false;
                } 
                else if(!string.IsNullOrEmpty(command.LocationToResponse) && !File.Exists(command.LocationToResponse))
                {
                    Console.WriteLine("File does not exist");
                    command.IsValid = false;
                }
            }
        }
    }
}
