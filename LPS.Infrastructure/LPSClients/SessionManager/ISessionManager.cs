#nullable enable

using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public interface ISessionManager
    {
        public void AddResponse(string sessionId, string variableName, ICapturedResponse response);
        public ICapturedResponse? GetResponse(string sessionId, string variableName);
        public void CleanupSession(string sessionId);
    }
}
