using FluentValidation;
using LPS.UI.Common.Options;

namespace LPS.UI.Core.LPSValidators
{
    internal class DashboardConfigurationValidator : AbstractValidator<DashboardConfigurationOptions>
    {
        public DashboardConfigurationValidator()
        {
            RuleFor(dashboard => dashboard.BuiltInDashboard)
                .NotNull()
                .WithMessage("'BuiltInDashboard' must be specified (true or false)");

            RuleFor(dashboard => dashboard.Port)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("'Port' must be greater than 0")
                .LessThan(65536)
                .WithMessage("'Port' must be less than 65536");

            RuleFor(dashboard => dashboard.RefreshRate)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("'RefreshRate' must be greater than 0 seconds");

            RuleFor(dashboard => dashboard.WindowIntervalSeconds)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("'WindowIntervalSeconds' must be greater than 0 seconds");
        }
    }
}
