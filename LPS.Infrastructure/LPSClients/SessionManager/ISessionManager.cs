#nullable enable

using LPS.Domain;
using LPS.Domain.Common.Interfaces;
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
        public Task PutVariableAsync(string sessionId, string variableName, IVariableHolder variableHolder, CancellationToken token);
        public Task<IVariableHolder?> GetVariableAsync(string sessionId, string variableName, CancellationToken token);
        public void CleanupSession(string sessionId);
    }
}
