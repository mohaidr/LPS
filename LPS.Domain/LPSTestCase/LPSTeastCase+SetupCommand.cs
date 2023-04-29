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
            }

            public void Execute(LPSTestCase entity)
            {
                entity?.Setup(this);
            }



            public bool IsValid { get; set; }
        }

        private void Setup(SetupCommand command)
        {
            _ = new Validator(this, command);

            if (command.IsValid)
            {

            }
        }
    }
}
