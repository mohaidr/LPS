using LPS.UI.Common;
using LPS.Domain;
using System;
using LPS.Domain.Common.Interfaces;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class ManualBuild : IBuilderService<TestPlan.SetupCommand, TestPlan>
    {
        IBaseValidator<TestPlan.SetupCommand, TestPlan> _validator;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public ManualBuild(
            IBaseValidator<TestPlan.SetupCommand, 
            TestPlan> validator,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _validator= validator;
            _logger= logger;
            _runtimeOperationIdProvider= runtimeOperationIdProvider;
        }

        static bool _skipOptionalFields = true;
        
        //This must be refactored one domain is refactored
        public TestPlan Build(TestPlan.SetupCommand lpsTestCommand)
        {
            _skipOptionalFields = AnsiConsole.Confirm("Do you want to skip the optional fields?");

            new TestPlanChallengeUserService(_skipOptionalFields, lpsTestCommand, _validator).Challenge();
            var lpsPlan = new TestPlan(lpsTestCommand, _logger, _runtimeOperationIdProvider); // it should validate and throw if the command is not valid

            foreach (var runCommand in lpsTestCommand.LPSRuns)
            {
                var runEntity = new HttpRun(runCommand, _logger, _runtimeOperationIdProvider); // must validate and throw if the command is not valid
                runEntity.SetHttpRequestProfile(new HttpRequestProfile(runCommand.LPSRequestProfile, _logger, _runtimeOperationIdProvider));
                lpsPlan.LPSRuns.Add(runEntity);
            }
            
            return lpsPlan;
        }
    }
}
