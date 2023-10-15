using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSHttpTestCase
    {
        new public class SetupCommand : ICommand<LPSHttpTestCase>, IValidCommand
        {

            public SetupCommand()
            {
                LPSRequest = new LPSHttpRequest.SetupCommand();
                LPSTestCaseSetUpCommand = new LPSTestCase.SetupCommand(); 
            }

            public void Execute(LPSHttpTestCase entity)
            {
                entity?.Setup(this);
            }

            public LPSHttpRequest.SetupCommand LPSRequest { get; set; }

            public int? RequestCount { get; set; }

            public int? Duration { get; set; }

            public int? BatchSize { get; set; }

            public int? CoolDownTime { get; set; }

            public bool IsValid { get; set; }

            public string Name { get; set; }

            public IterationMode? Mode { get; set; }
            public IDictionary<string, string> ValidationErrors { get; set ; }

            // This will represrnt the parent entity SetupCommand. 
            // There will be no inheritance between Commands and Validators to avoid complexity and to tight every entity to its own commands
            // Only handled internal, public callers should not know about it
            internal LPSTestCase.SetupCommand LPSTestCaseSetUpCommand;
        }

        private void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setup command
            command.LPSTestCaseSetUpCommand = new LPSTestCase.SetupCommand() { Name = command.Name };
            command.LPSTestCaseSetUpCommand.Execute(this);
            new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && command.LPSTestCaseSetUpCommand.IsValid)
            {
                this.RequestCount = command.RequestCount;
                this.Mode = command.Mode;
                this.LPSHttpRequest = new LPSHttpRequest(command.LPSRequest, _logger, _resourceUsageTracker, _runtimeOperationIdProvider);
                this.Duration = command.Duration;
                this.CoolDownTime = command.CoolDownTime; ;
                this.BatchSize = command.BatchSize;
                this.IsValid = true;
            }
        }

        internal void Clone(LPSHttpTestCase cloneToEntity)
        {
            if (this.IsValid)
            {
                cloneToEntity.Id = this.Id;
                cloneToEntity.RequestCount = this.RequestCount;
                cloneToEntity.Name = this.Name;
                cloneToEntity.Mode = this.Mode;
                cloneToEntity.LPSHttpRequest = new LPSHttpRequest(_logger, _resourceUsageTracker, _runtimeOperationIdProvider);
                this.LPSHttpRequest.Clone(cloneToEntity.LPSHttpRequest);
                cloneToEntity.Duration = this.Duration;
                cloneToEntity.CoolDownTime = this.CoolDownTime; ;
                cloneToEntity.BatchSize = this.BatchSize;
                cloneToEntity.IsValid = true;
            }
        }
    }
}
