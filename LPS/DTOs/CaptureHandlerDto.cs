using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.LPSFlow.LPSHandlers;
using LPS.Infrastructure.VariableServices.VariableHolders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LPS.DTOs
{
    public class CaptureHandlerDto : IDto<CaptureHandlerDto>
    {
        public CaptureHandlerDto()
        {
            To = string.Empty;
            Regex = string.Empty;
            MakeGlobal = "false"; // Support placeholders for boolean values
        }

        // Name of the capture handler
        public string To { get; set; }

        // Type information
        // Type information
        private string? _as;
        public string As
        {
            get => _as ?? VariableType.String.ToString();
            set
            {
                _as = value;

            }
        }
        // Whether the capture should be global (supports placeholders)
        public string MakeGlobal { get; set; }

        // Regex pattern for capturing
        public string Regex { get; set; }


        // Deep copy method
        public void DeepCopy(out CaptureHandlerDto targetDto)
        {
            targetDto = new CaptureHandlerDto
            {
                To = this.To,
                As = this.As,
                Regex = this.Regex,
                MakeGlobal = this.MakeGlobal,
            };
        }
    }

}
