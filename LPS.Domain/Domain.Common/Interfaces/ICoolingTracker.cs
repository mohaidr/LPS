using System;
using System.Collections.Generic;

namespace LPS.Domain.Common.Interfaces;

public interface ICoolingTracker
{
    IDisposable BeginBatchCooldown(Guid iterationId, string hostName);
    void SetWatchdogState(string hostName, ResourceState state);
    IReadOnlyList<CoolingPeriod> GetPeriods(string hostName, DateTime start, DateTime end, Guid? iterationId = null);
}