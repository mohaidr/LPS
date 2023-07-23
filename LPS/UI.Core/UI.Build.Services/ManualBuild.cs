using LPS.UI.Common;
using LPS.Domain;
using System;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class ManualBuild : IBuilderService<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        IUserValidator<LPSTestPlan.SetupCommand, LPSTestPlan> _validator;
        public ManualBuild(IUserValidator<LPSTestPlan.SetupCommand, LPSTestPlan> validator)
        {
            _validator= validator;
        }

        static bool skipOptionalFields = true;

        public void Build(LPSTestPlan.SetupCommand lpsTestCommand)
        {
            Console.WriteLine("To skip optional fields enter (Y), otherwise enter (N)");
            string decision = Console.ReadLine();

            if (!string.IsNullOrEmpty(decision) && decision.Trim().ToLower() == "n")
                skipOptionalFields = false;
            else
                skipOptionalFields = true;

            new LPSTestPlanChallengeUserService(skipOptionalFields, lpsTestCommand, _validator).Challenge();
        }
    }
}
