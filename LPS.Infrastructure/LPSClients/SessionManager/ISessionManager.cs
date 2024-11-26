#nullable enable

using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public interface ISessionManager
    {
        public Task AddResponseAsync(string sessionId, string variableName, IVariableHolder response, CancellationToken token);
        public IVariableHolder? GetResponse(string sessionId, string variableName);
        public void CleanupSession(string sessionId);
    }
}
