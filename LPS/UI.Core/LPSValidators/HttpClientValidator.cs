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
using LPS.Infrastructure.LPSClients.HeaderServices;

namespace LPS.UI.Core.LPSValidators
{
    internal class HttpClientValidator : AbstractValidator<HttpClientOptions>
    {
        public HttpClientValidator()
        {
            RuleFor(httpClient => httpClient.ClientTimeoutInSeconds)
                .NotNull().WithMessage("'Client Timeout In Second' must be a non-null value")
                .GreaterThan(0).WithMessage("'Client Timeout In Second' must be greater than 0");
            RuleFor(httpClient => httpClient.PooledConnectionLifeTimeInSeconds)
                .NotNull().WithMessage("'Pooled Connection Life Time In Seconds' must be a non-null value")
                .GreaterThan(0).WithMessage("'Pooled Connection Life Time In Seconds' must be greater than 0");
            RuleFor(httpClient => httpClient.PooledConnectionIdleTimeoutInSeconds)
                .NotNull().WithMessage("'Pooled Connection Idle Timeout In Seconds' must be a non-null value")
                .GreaterThan(0).WithMessage("'Pooled Connection Idle Timeout In Seconds' must be greater than 0");
            RuleFor(httpClient => httpClient.MaxConnectionsPerServer)
                .NotNull().WithMessage("'Max Connections Per Server' a non-null value")
                .GreaterThan(0).WithMessage("'Max Connections Per Server' must be greater than 0");
           
            // NEW: Enum is valid (Strict | Lenient | RawPassthrough)
            RuleFor(http => http.HeaderValidationMode).NotNull()
                .IsInEnum().WithMessage("'Header Validation Mode' must be one of: Strict, Lenient, RawPassthrough");

            // NEW: Host override not allowed in Strict mode
            RuleFor(http => http.AllowHostOverride)
                .NotNull()
                .Must((opts, allow) => allow != true || opts.HeaderValidationMode != HeaderValidationMode.Strict)
                .WithMessage("'Allow Host Override' cannot be true when 'Header Validation Mode' is Strict.");

            // NEW: Cross-field sanity — idle timeout should not exceed lifetime
            When(http => http.PooledConnectionIdleTimeoutInSeconds.HasValue &&
                         http.PooledConnectionLifeTimeInSeconds.HasValue, () =>
                         {
                             RuleFor(http => http)
                             .Must(h => h.PooledConnectionIdleTimeoutInSeconds!.Value
                                        <= h.PooledConnectionLifeTimeInSeconds!.Value)
                             .WithMessage("'Pooled Connection Idle Timeout In Seconds' must be less than or equal to 'Pooled Connection Life Time In Seconds'.");
                         });
        }
    }
}
