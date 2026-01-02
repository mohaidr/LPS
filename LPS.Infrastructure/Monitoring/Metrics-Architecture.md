# Metrics Monitoring Architecture

## Overview

The metrics monitoring system collects, aggregates, and pushes real-time metrics to the dashboard during load test execution.

## Flow

1. **Coordinators start**
   - `WindowedMetricsCoordinator` and `CumulativeMetricsCoordinator` are singletons that run timers (configured intervals)

2. **Aggregators are created on schedule using `MetricsDataMonitor.TryRegisterAsync`**
   - Called when an iteration is scheduled to run
   - Creates 4 cumulative aggregators + 4 windowed aggregators per iteration
   - Aggregators: Duration, Throughput, ResponseCode, DataTransmission

3. **TryRegisterAsync creates collectors (one windowed + one cumulative per iteration)**
   - Each collector subscribes to its coordinator's event
   - Collector has logic to get data and push it to the queue:
     - **Cumulative collector**: Reads from `MetricDataStore` (aggregators already push data there)
     - **Windowed collector**: Reads directly from windowed aggregators, then resets them (unless final snapshot)

4. **During execution, the client pushes data to the aggregators**
   - HTTP client calls aggregator methods after each request
   - Cumulative aggregators write to `MetricDataStore`
   - Windowed aggregators keep data in memory

5. **Coordinator fires event → subscribed collectors push data to the queue**
   - Coordinator timer fires → `OnWindowClosed` / `OnPushInterval` event
   - All subscribed collectors receive the event
   - Each collector builds a snapshot and pushes to its queue

6. **Pushers consume from queue and push to dashboard via SignalR**
   - `WindowedMetricsPusher` and `CumulativeMetricsPusher` consume from queues
   - Push snapshots to SignalR hub → connected dashboard clients

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `MetricsDataMonitor` | MetricsServices/ | Entry point for registration |
| `MetricAggregatorFactory` | MetricsServices/ | Creates and caches aggregators |
| `WindowedMetricsCoordinator` | Windowed/ | Timer for windowed collection |
| `CumulativeMetricsCoordinator` | Cumulative/ | Timer for cumulative collection |
| `WindowedIterationMetricsCollector` | Windowed/ | Collects windowed data per iteration |
| `CumulativeIterationMetricsCollector` | Cumulative/ | Collects cumulative data per iteration |
| `MetricDataStore` | MetricsServices/ | Stores cumulative metric snapshots |
| `*MetricsPusher` | Windowed/ & Cumulative/ | Push to SignalR |

## Data Flow Diagram

```
HTTP Client
    │
    ▼
Aggregators ──────────────────┐
    │                         │
    │ (cumulative)            │ (windowed - in memory)
    ▼                         │
MetricDataStore               │
    │                         │
    └────────┬────────────────┘
             │
             ▼
      Collectors (per iteration)
             │
             │ ◄── Coordinator fires event
             ▼
         Queues
             │
             ▼
         Pushers
             │
             ▼
    Dashboard (SignalR)
```
