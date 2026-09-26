namespace LPS.Domain
{
    public readonly record struct ClientSchedule(
        int StageIndex,
        int ClientIndex,
        long ArrivalOffsetMs,
        int StartupDelayMs);
}