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
using static AsyncTest.Domain.HttpAsyncRequestWrapper;
using AsyncTest.Domain.Common;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncTest
    {
        public class RedoCommand : ICommand<HttpAsyncTest>
        {
            public void Execute(HttpAsyncTest entity)
            {
                throw new NotImplementedException();
            }

            async public Task ExecuteAsync(HttpAsyncTest entity)
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

