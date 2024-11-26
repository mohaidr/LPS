using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net;
using FluentValidation;
using FluentValidation.Results;
using LPS.UI.Common;
using System.Text;
using Microsoft.AspNetCore.Http;
using LPS.DTOs;
using System.CommandLine;
using LPS.Domain.LPSFlow.LPSHandlers;

namespace LPS.UI.Core.LPSValidators
{
    public class CaptureValidator : CommandBaseValidator<CaptureHandlerDto, CaptureHandler>
    {

        readonly CaptureHandlerDto _captureHandlerDto;
        public CaptureValidator(CaptureHandlerDto captureHandlerDto)
        {
            ArgumentNullException.ThrowIfNull(captureHandlerDto);
            _captureHandlerDto = captureHandlerDto;
            RuleFor(dto => dto.Variable)
                .NotNull()
                .NotEmpty();
            RuleFor(dto => dto.MakeGlobal)
                .NotNull();
            RuleFor(dto => dto.As)
                .Must(@as =>
                {
                    @as ??= string.Empty;
                    return @as.Equals("JSON", StringComparison.OrdinalIgnoreCase)
                    || @as.Equals("XML", StringComparison.OrdinalIgnoreCase)
                    || @as.Equals("Text", StringComparison.OrdinalIgnoreCase);
                });
            RuleFor(dto => dto.Regex)
            .Must(regex => string.IsNullOrEmpty(regex) || IsValidRegex(regex))
            .WithMessage("Input must be either empty or a valid .NET regular expression.");
        }

        public override CaptureHandlerDto Dto { get { return _captureHandlerDto; } }

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

