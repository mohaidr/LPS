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
            }

            public void Execute(LPSHttpTestCase entity)
            {
                entity?.Setup(this);
            }

            public LPSHttpRequest.SetupCommand LPSRequest { get; set; }
            public LPSTestPlan.SetupCommand Plan { get; private set; }

            public int? RequestCount { get; set; }

            public int? Duration { get; set; }

            public int? BatchSize { get; set; }

            public int? CoolDownTime { get; set; }

            public bool IsValid { get; set; }

            public string Name { get; set; }

            public IterationMode? Mode { get; set; }
            public IDictionary<string, string> ValidationErrors { get; set ; }
        }

        private void Setup(SetupCommand command)
        {
            _ = new Validator(this, command, _logger, _runtimeOperationIdProvider);

            if (command.IsValid)
            {
                this.RequestCount = command.RequestCount;
                this.Name = command.Name;
                this.Mode = command.Mode;
                this.LPSHttpRequest = new LPSHttpRequest(command.LPSRequest, this._logger, _runtimeOperationIdProvider);
                this.Duration = command.Duration;
                this.CoolDownTime = command.CoolDownTime; ;
                this.BatchSize = command.BatchSize;
                this.IsValid = true;
            }
        }
    }
}
