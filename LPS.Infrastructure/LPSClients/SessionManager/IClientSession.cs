#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public interface IClientSession
    {
        public void AddResponse(string variableName, IVariableHolder response);
        public IVariableHolder? GetResponse(string variableName);
    }
}
