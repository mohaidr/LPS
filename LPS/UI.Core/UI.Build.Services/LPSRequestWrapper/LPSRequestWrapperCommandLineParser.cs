using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestWrapperCommandLineParser : IParser<LPSRequestWrapper.SetupCommand, LPSRequestWrapper>
    {
        public string[] CommandLineArgs { get; set; }

        public LPSRequestWrapperCommandLineParser()
        {
        }

        public void Parse(LPSRequestWrapper.SetupCommand command)
        {

            for (int i = 0; i < CommandLineArgs.Length; i++)
            {
                switch (CommandLineArgs[i])
                {
                    case "-name":
                    case "-n":
                        command.Name = CommandLineArgs[++i];
                        break;

                    case "-repeat":
                    case "-r":
                        try
                        {
                            command.NumberofAsyncRepeats = int.Parse(CommandLineArgs[++i]);
                        }
                        catch
                        {
                            command.NumberofAsyncRepeats = -1;
                            CommandLineArgs[i] = command.NumberofAsyncRepeats.ToString();
                        }
                        break;
                    default:
                        new LPSRequestCommandLineParser() { CommandLineArgs = this.CommandLineArgs }.Parse(command.LPSRequest);
                        break;
                }
                var userService = new LPSRequestWrapperUserService(true, command, new LPSRequestWrapperValidator(command));
                userService.Challenge();
            }
        }
    }
}
