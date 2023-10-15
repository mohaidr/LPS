using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    internal class CancellationTokenWrapper : ICancellationTokenWrapper
    {
        private CancellationToken _token;

        public CancellationTokenWrapper(CancellationToken token)
        { 
            _token= token;
        }
        public CancellationToken CancellationToken => _token;
    }
}
