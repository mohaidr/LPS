using FluentValidation;
using LPS.DTOs;
using LPS.Domain.Domain.Common.Extensions;
using LPS.Domain.Domain.Common.Enums;
using System.CommandLine;

namespace LPS.UI.Core.LPSValidators
{
    public class CaptureValidator : CommandBaseValidator<CaptureHandlerDto>
    {

        readonly CaptureHandlerDto _dto;
        public CaptureValidator(CaptureHandlerDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);
            _dto = dto;
            RuleFor(dto => dto.To)
                .NotNull().NotEmpty()
                .WithMessage("'Variable Name' must not be empty")
                .Matches("^[a-zA-Z0-9]+$")
                .WithMessage("'Variable Name' must only contain letters and numbers.");

            RuleFor(dto => dto.MakeGlobal)
                .NotNull()
                .WithMessage("The 'MakeGlobal' property must not be null.")
                .Must(makeGlobal =>
                {
                    // Allow valid boolean values or placeholders
                    return makeGlobal.StartsWith("$") || bool.TryParse(makeGlobal, out _);
                })
                .WithMessage("The 'MakeGlobal' property must be 'true', 'false', or a placeholder starting with '$'");

            RuleFor(dto => dto.As)
                    .Must(@as =>
                    {
                        return @as.TryToVariableType(out VariableType type) &&
                        (type == VariableType.String || type == VariableType.JsonString || type == VariableType.XmlString || type == VariableType.CsvString);
                    }).WithMessage($"The provided value for 'As' ({_dto.As}) is not valid or supported.");


            RuleFor(dto => dto.Regex)
            .Must(regex => string.IsNullOrEmpty(regex) || IsValidRegex(regex))
            .WithMessage("Input must be either empty or a valid .NET regular expression.");
        }

        public override CaptureHandlerDto Dto { get { return _dto; } }

        private static bool IsValidRegex(string pattern)
        {
            try
            {
                // If the Regex object can be created without exceptions, the pattern is valid
                _ = new System.Text.RegularExpressions.Regex(pattern);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

}

