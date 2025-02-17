using FluentValidation;
using LPS.Infrastructure.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Core.LPSValidators
{
    internal class ClusteredConfigurationValidator : AbstractValidator<ClusterConfiguration>
    {
        public ClusteredConfigurationValidator()
        {
            RuleFor(config => config.MasterNodeIP)
                .NotEmpty()
                .WithMessage("MasterNodeIP is required.");

            RuleFor(config => config.WorkerRegistrationPort)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("WorkerRegistrationPort must be a positive integer.");

            RuleFor(config => config.ExpectedNumberOfWorkers)
                .NotNull()
                .GreaterThanOrEqualTo(1)
                .WithMessage("ExpectedNumberOfWorkers must be at least 1.");
        }
    }

}
