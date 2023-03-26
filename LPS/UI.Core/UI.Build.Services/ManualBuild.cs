using LPS.UI.Common;
using LPS.Domain;
using System;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class ManualBuild : IBuilderService<LPSTest.SetupCommand, LPSTest>
    {
        IValidator<LPSTest.SetupCommand, LPSTest> _validator;
        public ManualBuild(IValidator<LPSTest.SetupCommand, LPSTest> validator)
        {
            _validator= validator;
        }

        static bool skipOptionalFields = true;

        public void Build(LPSTest.SetupCommand lpsTestCommand)
        {
            Console.WriteLine("To skip optional fields enter (Y), otherwise enter (N)");
            string decision = Console.ReadLine();

            if (!string.IsNullOrEmpty(decision) && decision.Trim().ToLower() == "n")
                skipOptionalFields = false;
            else
                skipOptionalFields = true;

            Console.WriteLine("Start building your collection of requests");
            new LPSTestChallengeUserService(skipOptionalFields, lpsTestCommand, _validator).Challenge();
        }
    }
}
