using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Threading;

namespace LPS.Infrastructure.LPSClients;

/// <summary>
/// Passively captures DNS, TCP, and TLS timing metrics from .NET runtime events
/// without interfering with the connection path.
/// Uses FIFO queues per host to support multiple concurrent connections.
/// </summary>
public sealed class HttpConnectionEventListener : EventListener
{
    private static readonly Lazy<HttpConnectionEventListener> _instance = new(() => new HttpConnectionEventListener());
    public static HttpConnectionEventListener Instance => _instance.Value;

    // FIFO queue of completed timings per host - final stage
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConnectionTimingInfo>> _timingsQueue = new();
    
    // Pipeline stages: DNS -> TCP -> TLS -> Completed
    // Each stage dequeues from previous, updates, enqueues to next
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConnectionTimingInfo>> _afterDnsQueue = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConnectionTimingInfo>> _afterTcpQueue = new();

    // Track in-flight operations as FIFO queues per hostname
    private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> _pendingDns = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> _pendingTcp = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> _pendingTls = new();

    private HttpConnectionEventListener() { }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        switch (eventSource.Name)
        {
            case "System.Net.NameResolution":
            case "System.Net.Sockets":
            case "System.Net.Security":
                EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
                break;
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName == null || eventData.Payload == null)
            return;

        try
        {
            switch (eventData.EventSource.Name)
            {
                case "System.Net.NameResolution":
                    HandleDnsEvent(eventData);
                    break;
                case "System.Net.Sockets":
                    HandleSocketEvent(eventData);
                    break;
                case "System.Net.Security":
                    HandleTlsEvent(eventData);
                    break;
            }
        }
        catch
        {
            // Silently ignore event processing errors to avoid affecting the main flow
        }
    }

    private void HandleDnsEvent(EventWrittenEventArgs eventData)
    {
        // ResolutionStart has hostname, ResolutionStop has EMPTY payload
        if (eventData.EventName == "ResolutionStart" && eventData.Payload?.Count > 0)
        {
            try
            {
                var hostName = eventData.Payload[0]?.ToString() ?? "";
                if (string.IsNullOrEmpty(hostName)) return;
                
                var dnsQueue = _pendingDns.GetOrAdd(hostName, _ => new ConcurrentQueue<double>());
                dnsQueue.Enqueue(GetTimestamp(eventData));
                
                Console.WriteLine($"[EVENT] DNS Start: {hostName} | Pending DNS: {dnsQueue.Count}");
            }
            catch (ArgumentOutOfRangeException) { /* Payload structure changed */ }
            catch (IndexOutOfRangeException) { /* Payload access failed */ }
        }
        else if (eventData.EventName == "ResolutionStop")
        {
            // ResolutionStop has no payload, find first host with pending DNS
            string resolvedHost = null;
            foreach (var kvp in _pendingDns)
            {
                if (!kvp.Value.IsEmpty)
                {
                    resolvedHost = kvp.Key;
                    break;
                }
            }
            
            if (resolvedHost != null && 
                _pendingDns.TryGetValue(resolvedHost, out var dnsQueue) && 
                dnsQueue.TryDequeue(out var startTime))
            {
                var duration = GetTimestamp(eventData) - startTime;
                
                // Create new timing entry and enqueue to afterDns stage
                var timing = new ConnectionTimingInfo { DnsResolutionMs = duration };
                var afterDnsQueue = _afterDnsQueue.GetOrAdd(resolvedHost, _ => new ConcurrentQueue<ConnectionTimingInfo>());
                afterDnsQueue.Enqueue(timing);
                
                Console.WriteLine($"[EVENT] DNS Stop: {resolvedHost} | Duration: {duration:F2}ms | Pending DNS: {dnsQueue.Count} | After DNS: {afterDnsQueue.Count}");
            }
        }
    }

    private void HandleSocketEvent(EventWrittenEventArgs eventData)
    {
        // ConnectStart/Stop have no useful payload for correlation
        // Strategy: Use FIFO matching with hosts that have timings from DNS stage
        if (eventData.EventName == "ConnectStart")
        {
            // Find the oldest host with DNS-completed timing
            foreach (var kvp in _afterDnsQueue)
            {
                if (!kvp.Value.IsEmpty)
                {
                    var tcpQueue = _pendingTcp.GetOrAdd(kvp.Key, _ => new ConcurrentQueue<double>());
                    tcpQueue.Enqueue(GetTimestamp(eventData));
                    Console.WriteLine($"[EVENT] TCP Start: {kvp.Key} | Pending TCP: {tcpQueue.Count}");
                    break;
                }
            }
        }
        else if (eventData.EventName == "ConnectStop")
        {
            // Find first host with pending TCP
            string host = null;
            foreach (var kvp in _pendingTcp)
            {
                if (!kvp.Value.IsEmpty)
                {
                    host = kvp.Key;
                    break;
                }
            }
            
            if (host != null && 
                _pendingTcp.TryGetValue(host, out var tcpQueue) && 
                tcpQueue.TryDequeue(out var startTime) &&
                _afterDnsQueue.TryGetValue(host, out var afterDnsQueue) &&
                afterDnsQueue.TryDequeue(out var timing))
            {
                var duration = GetTimestamp(eventData) - startTime;
                timing.TcpHandshakeMs = duration;
                
                // Move to afterTcp stage
                var afterTcpQueue = _afterTcpQueue.GetOrAdd(host, _ => new ConcurrentQueue<ConnectionTimingInfo>());
                afterTcpQueue.Enqueue(timing);
                
                Console.WriteLine($"[EVENT] TCP Stop: {host} | Duration: {duration:F2}ms | Pending TCP: {tcpQueue.Count} | After TCP: {afterTcpQueue.Count}");
            }
        }
    }
    
    private void HandleTlsEvent(EventWrittenEventArgs eventData)
    {
        // HandshakeStart / HandshakeStop events
        // System.Net.Security events: HandshakeStart has targetHost, HandshakeStop has various info
        if (eventData.EventName == "HandshakeStart" && eventData.Payload?.Count > 0)
        {
            try
            {
                // Try to get the target host from the payload
                string targetHost = "";
                var payloadCount = eventData.Payload.Count;
                
                for (int i = 0; i < payloadCount; i++)
                {
                    try
                    {
                        var val = eventData.Payload[i]?.ToString() ?? "";
                        // Skip booleans and short values, look for hostname
                        if (val.Length > 3 && val.Contains('.') && !val.StartsWith("True") && !val.StartsWith("False"))
                        {
                            targetHost = val;
                            break;
                        }
                    }
                    catch (ArgumentOutOfRangeException) { break; }
                    catch (IndexOutOfRangeException) { break; }
                }
                
                if (!string.IsNullOrEmpty(targetHost))
                {
                    var tlsQueue = _pendingTls.GetOrAdd(targetHost, _ => new ConcurrentQueue<double>());
                    tlsQueue.Enqueue(GetTimestamp(eventData));
                    Console.WriteLine($"[EVENT] TLS Start: {targetHost} | Pending TLS: {tlsQueue.Count}");
                }
            }
            catch { /* Ignore payload access errors */ }
        }
        else if (eventData.EventName == "HandshakeStop")
        {
            // Find first host with pending TLS
            string host = null;
            foreach (var kvp in _pendingTls)
            {
                if (!kvp.Value.IsEmpty)
                {
                    host = kvp.Key;
                    break;
                }
            }
            
            if (host != null && 
                _pendingTls.TryGetValue(host, out var tlsQueue) && 
                tlsQueue.TryDequeue(out var startTime) &&
                _afterTcpQueue.TryGetValue(host, out var afterTcpQueue) &&
                afterTcpQueue.TryDequeue(out var timing))
            {
                var duration = GetTimestamp(eventData) - startTime;
                timing.TlsHandshakeMs = duration;
                timing.LastUpdated = DateTime.UtcNow;
                
                // TLS is the last step - enqueue to completed timings
                var completedQueue = _timingsQueue.GetOrAdd(host, _ => new ConcurrentQueue<ConnectionTimingInfo>());
                completedQueue.Enqueue(timing);
                
                Console.WriteLine($"[EVENT] TLS Stop: {host} | Duration: {duration:F2}ms | Enqueued | Pending TLS: {tlsQueue.Count} | Completed: {completedQueue.Count}");
            }
        }
    }
    


    /// <summary>
    /// Gets and clears the next timing info for a host from the FIFO queue.
    /// Returns null if no timing was captured (e.g., connection was reused from pool).
    /// </summary>
    public ConnectionTimingInfo GetAndClearTiming(string host)
    {
        // Try to dequeue from the host's queue
        if (_timingsQueue.TryGetValue(host, out var queue) && queue.TryDequeue(out var timing))
        {
            return timing;
        }

        // Check for partial matches (in case of IP vs hostname)
        foreach (var key in _timingsQueue.Keys)
        {
            if (key.Contains(host) || host.Contains(key))
            {
                if (_timingsQueue.TryGetValue(key, out queue) && queue.TryDequeue(out timing))
                {
                    return timing;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the count of pending timings for a host. Useful for debugging.
    /// </summary>
    public int GetPendingCount(string host)
    {
        if (_timingsQueue.TryGetValue(host, out var queue))
        {
            return queue.Count;
        }
        return 0;
    }

    /// <summary>
    /// Gets timing info without clearing. Useful for debugging.
    /// </summary>
    public ConnectionTimingInfo PeekTiming(string host)
    {
        if (_timingsQueue.TryGetValue(host, out var queue) && queue.TryPeek(out var timing))
        {
            return timing;
        }
        return null;
    }

    /// <summary>
    /// Gets the event timestamp in milliseconds (uses event occurrence time, not processing time).
    /// </summary>
    private static double GetTimestamp(EventWrittenEventArgs eventData)
    {
        // Use the event's own timestamp (when it occurred) rather than DateTime.UtcNow (when we process it)
        // This eliminates EventListener thread scheduling delays from measurements
        return eventData.TimeStamp.Ticks / (double)TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// Cleans up stale entries older than the specified age.
    /// </summary>
    public void CleanupStaleEntries(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        
        // Clean up stale queued timings
        foreach (var kvp in _timingsQueue)
        {
            // Dequeue and discard stale entries
            while (kvp.Value.TryPeek(out var timing) && timing.LastUpdated < cutoff)
            {
                kvp.Value.TryDequeue(out _);
            }
            
            // Remove empty queues
            if (kvp.Value.IsEmpty)
            {
                _timingsQueue.TryRemove(kvp.Key, out _);
            }
        }
        
        // Clean up stale in-progress timings from pipeline stages
        foreach (var kvp in _afterDnsQueue)
        {
            while (kvp.Value.TryPeek(out var timing) && timing.LastUpdated < cutoff)
            {
                kvp.Value.TryDequeue(out _);
            }
            if (kvp.Value.IsEmpty)
            {
                _afterDnsQueue.TryRemove(kvp.Key, out _);
            }
        }
        
        foreach (var kvp in _afterTcpQueue)
        {
            while (kvp.Value.TryPeek(out var timing) && timing.LastUpdated < cutoff)
            {
                kvp.Value.TryDequeue(out _);
            }
            if (kvp.Value.IsEmpty)
            {
                _afterTcpQueue.TryRemove(kvp.Key, out _);
            }
        }
    }
}

public class ConnectionTimingInfo
{
    public double DnsResolutionMs { get; set; }
    public double TcpHandshakeMs { get; set; }
    public double TlsHandshakeMs { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
