using LPS.Domain.Common.Interfaces;
using System.Threading;

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
