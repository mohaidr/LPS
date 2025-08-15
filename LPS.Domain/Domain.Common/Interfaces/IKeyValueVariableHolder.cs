using LPS.Domain.Common.Interfaces;
using System.Collections.Generic;

namespace LPS.Domain.Domain.Common.Interfaces
{
    /// Represents a single alias→value entry.
    public interface IKeyValueVariableHolder : IObjectVariableHolder
    {
        KeyValuePair<string, IVariableHolder> KeyValue { get; }
    }
}
