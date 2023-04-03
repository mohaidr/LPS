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

    public partial class LPSTestCase
    {
        public class SetupCommand : ICommand<LPSTestCase>
        {

            public SetupCommand()
            {
                Name = DateTime.Now.Ticks.ToString();
                LPSRequest = new LPSRequest.SetupCommand();
            }

            public void Execute(LPSTestCase entity)
            {
                entity?.Setup(this);
            }

            public LPSRequest.SetupCommand LPSRequest { get; set; }

            public int? RequestCount { get; set; }

            public int? Duration { get; set; }

            public int? BatchSize { get; set; }

            public int? CoolDownTime { get; set; }

            public bool IsValid { get; set; }

            public string Name { get; set; }

            public IterationMode? Mode
            {
                get
                {

                    if (Duration.HasValue && Duration.Value > 0
                        && CoolDownTime.HasValue && CoolDownTime.Value > 0
                        && RequestCount.HasValue && RequestCount.Value > 0
                        && !BatchSize.HasValue)
                    {
                        return IterationMode.DCR;
                    }
                    else
                    if (Duration.HasValue && Duration.Value > 0
                        && CoolDownTime.HasValue && CoolDownTime.Value > 0
                        && BatchSize.HasValue && BatchSize.Value > 0
                        && !RequestCount.HasValue)
                    {
                        return IterationMode.DCB;
                    }
                    else
                    if (Duration.HasValue && Duration.Value > 0
                        && RequestCount.HasValue && RequestCount.Value > 0
                        && BatchSize.HasValue && BatchSize.Value > 0
                        && !CoolDownTime.HasValue)
                    {
                        return IterationMode.DRB;
                    }
                    else
                    if (CoolDownTime.HasValue && CoolDownTime.Value > 0
                        && RequestCount.HasValue && RequestCount.Value > 0
                        && BatchSize.HasValue && BatchSize.Value > 0
                        && !Duration.HasValue)
                    {
                        return IterationMode.CRB;
                    }
                    else
                    if (CoolDownTime.HasValue && CoolDownTime.Value > 0
                        && BatchSize.HasValue && BatchSize.Value > 0
                        && !Duration.HasValue
                        && !RequestCount.HasValue)
                    {
                        return IterationMode.CB;
                    }
                    else
                    if (RequestCount.HasValue && RequestCount.Value > 0
                        && !Duration.HasValue
                        && !BatchSize.HasValue
                        && !CoolDownTime.HasValue)
                    {
                        return IterationMode.R;
                    }

                    return null;
                }

            }

        }

        private void Setup(SetupCommand command)
        {
            _ = new Validator(this, command);

            if (command.IsValid)
            {
                this.Count = command.RequestCount;
                this.Name = command.Name;
                this.Mode = command.Mode;
                this.LPSRequest = new LPSRequest(command.LPSRequest, this._logger);
                this.Duration = command.Duration;
                this.CoolDownTime = command.CoolDownTime; ;
                this.BatchSize = command.BatchSize;
                this.IsValid = true;
            }
        }
    }
}
