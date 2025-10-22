using FluentValidation;
using LPS.Infrastructure.Nodes;
using LPS.UI.Common.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Core.LPSValidators
{
    internal class ClusteredConfigurationValidator : AbstractValidator<ClusterConfigurationOptions>
    {
        public ClusteredConfigurationValidator()
        {
            RuleFor(config => config.MasterNodeIP)
                .NotEmpty()
                .WithMessage("MasterNodeIP is required.");
           
            RuleFor(config => config.MasterNodeIsWorker)
            .NotNull()
            .WithMessage("MasterNodeIsWorker is required.");


            RuleFor(c => c.GRPCPort)
                .NotNull().WithMessage("GRPCPort is required.")
                .InclusiveBetween(1, 65535).WithMessage("GRPCPort must be between 1 and 65535.");


            RuleFor(config => config.ExpectedNumberOfWorkers)
                .NotNull()
                .GreaterThanOrEqualTo(1)
                .WithMessage("ExpectedNumberOfWorkers must be at least 1.");
        }
    }

}
