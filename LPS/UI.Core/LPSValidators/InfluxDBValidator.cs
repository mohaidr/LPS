using FluentValidation;
using LPS.UI.Common.Options;

namespace LPS.UI.Core.LPSValidators
{
    internal class InfluxDBValidator : AbstractValidator<InfluxDBOptions>
    {
        public InfluxDBValidator()
        {
            RuleFor(options => options.Enabled)
                .NotNull()
                .WithMessage("'Enabled' must be specified (true or false)");

            When(options => options.Enabled == true, () =>
            {
                RuleFor(options => options.Url)
                    .NotEmpty()
                    .WithMessage("'Url' is required when InfluxDB is enabled")
                    .Must(url => url != null && (url.StartsWith("http://") || url.StartsWith("https://")))
                    .WithMessage("'Url' must start with 'http://' or 'https://'");

                RuleFor(options => options.Token)
                    .NotEmpty()
                    .WithMessage("'Token' is required when InfluxDB is enabled");

                RuleFor(options => options.Organization)
                    .NotEmpty()
                    .WithMessage("'Organization' is required when InfluxDB is enabled");

                RuleFor(options => options.Bucket)
                    .NotEmpty()
                    .WithMessage("'Bucket' is required when InfluxDB is enabled");
            });
        }
    }
}
