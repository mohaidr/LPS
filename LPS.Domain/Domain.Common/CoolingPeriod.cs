using System;

namespace LPS.Domain;

public sealed record CoolingPeriod(string Source, DateTime Start, DateTime End);