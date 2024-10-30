using LPS.UI.Common;
using LPS.Domain;
using System;
using LPS.Domain.Common.Interfaces;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;

namespace LPS.UI.Core.Build.Services
{
    internal class ManualBuild : IBuilderService<Plan.SetupCommand, Plan>
    {
        IBaseValidator<Plan.SetupCommand, Plan> _validator;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public ManualBuild(
            IBaseValidator<Plan.SetupCommand,
            Plan> validator,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _validator= validator;
            _logger= logger;
            _runtimeOperationIdProvider= runtimeOperationIdProvider;
        }

        static bool _skipOptionalFields = true;
        
        //This must be refactored one domain is refactored
        public Plan Build(Plan.SetupCommand planTestCommand)
        {
            _skipOptionalFields = AnsiConsole.Confirm("Do you want to skip the optional fields?");

            new PlanChallengeUserService(_skipOptionalFields, planTestCommand, _validator).Challenge();
            var plan = new Plan(planTestCommand, _logger, _runtimeOperationIdProvider); // it should validate and throw if the command is not valid

            foreach (var roundCommand in planTestCommand.Rounds)
            {
                var roundEntity = new Round(roundCommand, _logger, _runtimeOperationIdProvider);
                plan.AddRound(roundEntity);
                foreach (var iterationCommand in roundCommand.Iterations)
                {
                    var iterationEntity = new HttpIteration(iterationCommand, _logger, _runtimeOperationIdProvider); // must validate and throw if the command is not valid
                    iterationEntity.SetHttpRequestProfile(new HttpRequestProfile(iterationCommand.RequestProfile, _logger, _runtimeOperationIdProvider));
                    roundEntity.AddIteration(iterationEntity);
                }
            }
            
            return plan;
        }
    }
}
