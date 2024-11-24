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
    public class ClientSession(string sessionId) : IClientSession
    {
        public string SessionId { get; } = sessionId;
        private readonly ConcurrentDictionary<string, IVariableHolder> _variables = new();

        public void AddResponse(string variableName, IVariableHolder response)
        {
            _variables[variableName] = response;
        }

        public IVariableHolder? GetResponse(string variableName)
        {
            return _variables.TryGetValue(variableName, out var response) ? response : null;
        }
    }
}
