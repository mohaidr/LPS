#nullable enable

using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public class SessionManager : ISessionManager
    {
        private readonly ConcurrentDictionary<string, IClientSession> _sessions = new();

        public void AddResponse(string sessionId, string variableName, IVariableHolder capturedResponse)
        {
            var session = _sessions.GetOrAdd(sessionId, _ => new ClientSession(sessionId));
            session.AddResponse(variableName, capturedResponse);
        }

        public IVariableHolder? GetResponse(string sessionId, string variableName)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session.GetResponse(variableName) : null;
        }

        public void CleanupSession(string clientId)
        {
            _sessions.TryRemove(clientId, out _);
        }
    }
}
