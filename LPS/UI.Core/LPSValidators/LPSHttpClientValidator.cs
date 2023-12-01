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

namespace LPS.UI.Core.LPSValidators
{
    internal class LPSHttpClientValidator : AbstractValidator<LPSHttpClientOptions>
    {
        public LPSHttpClientValidator()
        {
            RuleFor(httpClient => httpClient.ClientTimeoutInSeconds)
                .NotNull()
                .GreaterThan(0);
            RuleFor(httpClient => httpClient.PooledConnectionLifeTimeInSeconds)
                .NotNull()
                .GreaterThan(0);
            RuleFor(httpClient => httpClient.PooledConnectionIdleTimeoutInSeconds)
                .NotNull()
                .GreaterThan(0);
            RuleFor(httpClient => httpClient.MaxConnectionsPerServer)
                .NotNull()
                .GreaterThan(0);
        }
    }
}
