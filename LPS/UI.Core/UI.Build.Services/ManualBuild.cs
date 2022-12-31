using LPS.UI.Common;
using LPS.Domain;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
            new LPSTestUserService(skipOptionalFields, lpsTestCommand, _validator).Challenge();
        }
    }
}
