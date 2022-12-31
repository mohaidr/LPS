using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestCommandLineParser : IParser<LPSTest.SetupCommand, LPSTest>
    {
        public string[] CommandLineArgs { get; set; }

        public LPSTestCommandLineParser()
        {
        }

        public void Parse(LPSTest.SetupCommand command)
        {
            if (CommandLineArgs.Length == 0)
            {
                Console.WriteLine("can't parse 0 arguments");
            }

            CommandLineArgs = Array.ConvertAll(CommandLineArgs, arg => arg.ToLower());
            LPSRequestWrapper.SetupCommand lpsRequestWrapperCommand = new LPSRequestWrapper.SetupCommand();

            for (int i = 0; i < CommandLineArgs.Length; i++)
            {
                switch (CommandLineArgs[i])
                {
                    case "-add":
                    case "-a":
                        if (i > 2)
                        {
                           // _userService.Challenge();
                        }
                        lpsRequestWrapperCommand = new LPSRequestWrapper.SetupCommand();
                        command.LPSRequestWrappers.Add(lpsRequestWrapperCommand);
                        break;
                    case "-testname":
                    case "-tn":
                        command.Name = CommandLineArgs[++i];
                        break;
                    default:
                        new LPSRequestWrapperCommandLineParser() { CommandLineArgs = this.CommandLineArgs}.Parse(lpsRequestWrapperCommand);
                        break;
                }
            }
           // _userService.Challenge();
        }
    }
}
