using System.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public class LPSConnectionCounterEventListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        try
        {
            if (!eventSource.Name.Equals("lps.active.connections"))
            {
                return;
            }
            var args = new Dictionary<string, string?> { ["EventCounterIntervalSec"] = "1" };
            EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, args);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.InnerException?.Message);
        }
    }
    private static Dictionary<string, int> _hostActiveConnectionsCount = new Dictionary<string, int>();
    public int GetHostActiveConnectionsCount(string hostName)
    {
        if (_hostActiveConnectionsCount.Keys.Contains(hostName))
        {
            return _hostActiveConnectionsCount[hostName];
        }
        return 0;
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        try
        {
            string hostName = string.Empty;
            int activeConnectionCount = -1;
            if (eventData.EventId == 1)
            {
                hostName = (string)eventData.Payload[0];
                activeConnectionCount = (int)eventData.Payload[1];
            }
            else if (eventData.EventId == 2)
            {
                hostName = (string)eventData.Payload[0];
                activeConnectionCount = (int)eventData.Payload[1];
            }
            if (!string.IsNullOrEmpty(hostName) && activeConnectionCount >= 0)
            {
                if (!_hostActiveConnectionsCount.Keys.Contains(hostName))
                {
                    _hostActiveConnectionsCount.Add(hostName, activeConnectionCount);
                }
                else
                {
                    _hostActiveConnectionsCount[hostName] = activeConnectionCount;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.InnerException?.Message);
        }
    }
}
