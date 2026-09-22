namespace LPS.UI.Core.Host
{
    public interface IFinalizationDisplay
    {
        void Start(TimeSpan estimatedDuration);
        Task ShowUntilShutdownAsync(Task shutdownTask);
    }
}