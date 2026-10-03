using LPS.Common.Interfaces;

namespace LPS.Common.Services
{
    public class TestTriggerNotifier : ITestTriggerNotifier
    {
        private readonly List<ITestTriggerObserver> _observers = new();
        private readonly object _gate = new();

        public void RegisterObserver(ITestTriggerObserver observer)
        {
            lock (_gate)
            {
                if (!_observers.Contains(observer))
                    _observers.Add(observer);
            }
        }

        public void UnregisterObserver(ITestTriggerObserver observer)
        {
            lock (_gate)
                _observers.Remove(observer);
        }

        public async Task NotifyObserversAsync()
        {
            ITestTriggerObserver[] observers;
            lock (_gate)
            {
                observers = _observers.ToArray();
                _observers.Clear();
            }
            await Task.WhenAll(observers.Select(observer => observer.OnTestTriggered()));
        }
    }
}