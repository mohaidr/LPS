using System;
using System.Collections.Generic;

namespace LPS.Domain.Common.Interfaces;

public interface ICoolingTracker
{
    /// <summary>
    /// Records a cooldown scope owned and disposed by the batch execution mode.
    /// </summary>
    IDisposable BeginBatchCooldown(Guid iterationId, string hostName, string reason = "Configured batch cooldown");
    void SetWatchdogState(string hostName, ResourceState state, string reason = "Resource pressure");
    IReadOnlyList<CoolingPeriod> GetPeriods(string hostName, DateTime start, DateTime end, Guid? iterationId = null);
    IReadOnlyList<CoolingUpdate> GetUpdates(DateTime since, DateTime until);
    void ApplyUpdate(CoolingUpdate update);
    void Complete();
}