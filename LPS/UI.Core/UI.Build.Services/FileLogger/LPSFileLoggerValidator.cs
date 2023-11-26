using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentValidation;
using LPS.UI.Common;
using LPS.Infrastructure.Logger;
using System.IO;
using LPS.UI.Common.Options;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSFileLoggerValidator : AbstractValidator<LPSFileLoggerOptions>
    {
        public LPSFileLoggerValidator()
        {
            RuleFor(logger => logger.LogFilePath)
            .NotNull().NotEmpty()
            .Matches(@"^(\/{0,1}(?!\/))[A-Za-z0-9\/\-_]+(\.([a-zA-Z]+))?$")
            .WithMessage("Invalid File Path");
        }
    }
}
