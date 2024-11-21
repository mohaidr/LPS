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
        private readonly ConcurrentDictionary<string, ICapturedResponse> _responses = new();

        public void AddResponse(string variableName, ICapturedResponse response)
        {
            _responses[variableName] = response;
        }

        public ICapturedResponse? GetResponse(string variableName)
        {
            return _responses.TryGetValue(variableName, out var response) ? response : null;
        }
    }
}
