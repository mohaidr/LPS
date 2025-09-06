using System;
using System.Text.Json.Serialization;

namespace LPS.Infrastructure.Common.Interfaces
{

    public interface IMetricShapshot
    {
        public DateTime TimeStamp { get; }

    }
}
