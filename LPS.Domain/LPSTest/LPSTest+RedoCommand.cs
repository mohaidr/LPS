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
using static LPS.Domain.LPSRequestWrapper;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTest
    {
        public class RedoCommand : ICommand<LPSTest>
        {
            public void Execute(LPSTest entity)
            {
                throw new NotImplementedException();
            }

            async public Task ExecuteAsync(LPSTest entity)
            {
                await entity.RedoAsync(this);
            }
        }
        async private Task RedoAsync(RedoCommand dto)
        {
            if (this.IsValid)
            {
                this.IsRedo = true;
                await this.ExecuteAsync(new ExecuteCommand());
            }
        }
    }
}

