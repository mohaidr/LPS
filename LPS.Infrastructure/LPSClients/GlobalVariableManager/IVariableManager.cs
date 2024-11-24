#nullable enable
using LPS.Infrastructure.LPSClients.SessionManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.GlobalVariableManager
{
    public interface IVariableManager
    {
        void AddVariable(string variableName, IVariableHolder variableHolder);
        IVariableHolder GetVariable(string variableName);
    }

}
