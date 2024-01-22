using LPS.Domain.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace LPS.UI.Common
{
    internal interface ILPSCLICommand
    {
        void Execute(CancellationToken cancellationToken);
    }
}
