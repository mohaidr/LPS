using LPS.UI.Common;
using LPS.Domain;
using System;
using LPS.Domain.Common.Interfaces;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;
using LPS.DTOs;

namespace LPS.UI.Core.Build.Services
{
    internal class ManualBuild : IBuilderService<PlanDto, Plan>
    {
        IBaseValidator<PlanDto, Plan> _validator;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public ManualBuild(
            IBaseValidator<PlanDto,
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
        public Plan Build(PlanDto planDto)
        {
            _skipOptionalFields = AnsiConsole.Confirm("Do you want to skip the optional fields?");

            new PlanChallengeUserService(_skipOptionalFields, planDto, _validator).Challenge();
            var plan = new Plan(planDto, _logger, _runtimeOperationIdProvider); // it should validate and throw if the command is not valid

            foreach (var roundCommand in planDto.Rounds)
            {
                var roundEntity = new Round(roundCommand, _logger, _runtimeOperationIdProvider);
                plan.AddRound(roundEntity);
                foreach (var iterationCommand in roundCommand.Iterations)
                {
                    var iterationEntity = new HttpIteration(iterationCommand, _logger, _runtimeOperationIdProvider); // must validate and throw if the command is not valid
                    iterationEntity.SetHttpRequest(new HttpRequest(iterationCommand.HttpRequest, _logger, _runtimeOperationIdProvider));
                    roundEntity.AddIteration(iterationEntity);
                }
            }
            
            return plan;
        }
    }
}
