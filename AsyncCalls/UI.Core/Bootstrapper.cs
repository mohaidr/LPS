using AsyncCalls.UI.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncCalls.UI.Core.UI.Build.Services;
using AsyncTest.Domain;
using Microsoft.Extensions.Hosting;
using AsyncTest.Domain.Common;
using Microsoft.Extensions.Configuration;
using AsyncTest.UI.Core;

namespace AsyncCalls.UI.Core
{
    internal class Bootstrapper : IBootStrapper
    {
        IFileLogger _loggger;
        IConfiguration _config;
        string[] _args;
        public Bootstrapper(IFileLogger loggger, IConfiguration config, dynamic cmdArgs)
        {
            _loggger = loggger;
            _config = config;
            _args = cmdArgs.args;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_args != null && _args.Length >= 0)
            { 
                
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
