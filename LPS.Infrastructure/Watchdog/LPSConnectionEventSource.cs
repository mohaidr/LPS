using System;
using System.Diagnostics.Tracing;
using System.Threading;

[EventSource(Name = "lps.active.connections")]
public class LPSConnectionEventSource : EventSource
{
    private static readonly Lazy<LPSConnectionEventSource> lazyInstance = new Lazy<LPSConnectionEventSource>(() => new LPSConnectionEventSource());
    private static SemaphoreSlim semaphore = new SemaphoreSlim(1);

    public static LPSConnectionEventSource Log => lazyInstance.Value;

    private LPSConnectionEventSource() { } // Private constructor to prevent external instantiation

    // Track the active connection count
    private static int activeConnectionCount = 0;

    [Event(1, Message = "Connection established: {0}, Active connection count: {1}")]
    public void ConnectionEstablished(string hostName, int numberOfActiveConnections = -1)
    {
        semaphore.Wait();
        try
        {
            activeConnectionCount = numberOfActiveConnections != -1 ? numberOfActiveConnections : Interlocked.Increment(ref activeConnectionCount);
            WriteEvent(1, hostName, activeConnectionCount);
        }
        finally
        {
            semaphore.Release();
        }
    }

    [Event(2, Message = "Connection closed: {0}, Active connection count: {1}")]
    public void ConnectionClosed(string hostName, int numberOfActiveConnections = -1)
    {
        semaphore.Wait();
        try
        {
            activeConnectionCount= numberOfActiveConnections != -1? numberOfActiveConnections : Interlocked.Decrement(ref activeConnectionCount);
            WriteEvent(2, hostName, activeConnectionCount);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
