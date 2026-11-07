using System;
using System.Collections.Generic;

namespace LPS.Domain.Common.Interfaces;

public interface ICoolingTracker
{
    void SetWatchdogState(string hostName, ResourceState state, string reason = "Resource pressure");
    IReadOnlyList<CoolingPeriod> GetPeriods(string hostName, DateTime start, DateTime end);
    IReadOnlyList<CoolingUpdate> GetUpdates(DateTime since, DateTime until);
    void ApplyUpdate(CoolingUpdate update);
    void Complete();
}