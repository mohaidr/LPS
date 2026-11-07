namespace LPS.Domain;

public sealed record CoolingUpdate(string HostName, CoolingPeriod Period, bool IsActive);