using LPS.UI.Common;
using LPS.Domain;
using System;
using LPS.Domain.Common.Interfaces;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class ManualBuild : IBuilderService<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        ILPSBaseValidator<LPSTestPlan.SetupCommand, LPSTestPlan> _validator;
        ILPSLogger _logger;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public ManualBuild(
            ILPSBaseValidator<LPSTestPlan.SetupCommand, 
            LPSTestPlan> validator,
            ILPSLogger logger,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _validator= validator;
            _logger= logger;
            _runtimeOperationIdProvider= runtimeOperationIdProvider;
        }

        static bool _skipOptionalFields = true;
        
        //This must be refactored one domain is refactored
        public LPSTestPlan Build(LPSTestPlan.SetupCommand lpsTestCommand)
        {
            _skipOptionalFields = AnsiConsole.Confirm("Do you want to skip the optional fields?");

            new LPSTestPlanChallengeUserService(_skipOptionalFields, lpsTestCommand, _validator).Challenge();
            var lpsPlan = new LPSTestPlan(lpsTestCommand, _logger, _runtimeOperationIdProvider); // it should validate and throw if the command is not valid

            foreach (var runCommand in lpsTestCommand.LPSRuns)
            {
                var runEntity = new LPSHttpRun(runCommand, _logger, _runtimeOperationIdProvider); // must validate and throw if the command is not valid
                runEntity.LPSHttpRequestProfile = new LPSHttpRequestProfile(runCommand.LPSRequestProfile, _logger, _runtimeOperationIdProvider);
                lpsPlan.LPSRuns.Add(runEntity);
            }
            
            return lpsPlan;
        }
    }
}
