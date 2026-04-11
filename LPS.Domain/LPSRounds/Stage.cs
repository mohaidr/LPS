namespace LPS.Domain
{
    public readonly struct Stage
    {
        public Stage(int numberOfClients, int arrivalDelay, int startupDelay = 0)
        {
            NumberOfClients = numberOfClients;
            ArrivalDelay = arrivalDelay;
            StartupDelay = startupDelay;
        }

        public int NumberOfClients { get; }
        public int ArrivalDelay { get; }
        public int StartupDelay { get; }
    }
}
