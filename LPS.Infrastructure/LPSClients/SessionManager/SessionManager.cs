#nullable enable

using LPS.Domain;
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

        public void AddResponse(string clientId, string variableName, ICapturedResponse capturedResponse)
        {
            var session = _sessions.GetOrAdd(clientId, _ => new ClientSession(clientId));
            session.AddResponse(variableName, capturedResponse);
        }

        public ICapturedResponse? GetResponse(string clientId, string variableName)
        {
            return _sessions.TryGetValue(clientId, out var session) ? session.GetResponse(variableName) : null;
        }

        public void CleanupSession(string clientId)
        {
            _sessions.TryRemove(clientId, out _);
        }
    }
}
