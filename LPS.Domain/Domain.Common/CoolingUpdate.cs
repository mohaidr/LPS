using System;

namespace LPS.Domain;

public sealed record CoolingUpdate(Guid IterationId, string HostName, CoolingPeriod Period, bool IsActive);