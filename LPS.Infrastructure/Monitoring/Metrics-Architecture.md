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
   - Collectors are wired up with their respective aggregators
   - Both collectors read directly from aggregators (consistent pattern):
     - **Cumulative collector**: Reads from cumulative aggregators via `GetCumulativeData()` (does NOT reset)
     - **Windowed collector**: Reads from windowed aggregators via `GetWindowDataAndReset()` (resets after read)

4. **During execution, the client pushes data to the aggregators**
   - HTTP client calls aggregator methods after each request
   - Cumulative aggregators also write to `LiveMetricDataStore` for real-time queries (UI, gRPC)
   - Windowed aggregators keep data in memory only

5. **Coordinator fires event → subscribed collectors push data to the queue**
   - Coordinator timer fires → `OnWindowClosed` / `OnPushInterval` event
   - All subscribed collectors receive the event
   - Each collector reads from its aggregators and builds a snapshot
   - Snapshot is pushed to its queue

6. **Pushers consume from queue and push to dashboard via SignalR**
   - `WindowedMetricsPusher` and `CumulativeMetricsPusher` consume from queues
   - Push snapshots to SignalR hub → connected dashboard clients

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `MetricsDataMonitor` | MetricsServices/ | Entry point for registration, wires aggregators to collectors |
| `MetricAggregatorFactory` | MetricsServices/ | Creates and caches aggregators |
| `WindowedMetricsCoordinator` | Windowed/ | Timer for windowed collection |
| `CumulativeMetricsCoordinator` | Cumulative/ | Timer for cumulative collection |
| `WindowedIterationMetricsCollector` | Windowed/ | Collects windowed data from aggregators per iteration |
| `CumulativeIterationMetricsCollector` | Cumulative/ | Collects cumulative data from aggregators per iteration |
| `LiveMetricDataStore` | MetricsServices/ | Real-time metric store for UI/gRPC queries (reduced capacity: 256) |
| `WindowedMetricDataStore` | Windowed/ | Stores windowed snapshots history |
| `*MetricsPusher` | Windowed/ & Cumulative/ | Push to SignalR |

## Data Flow Diagram

```
HTTP Client
    │
    ▼
Aggregators (Cumulative + Windowed)
    │                         │
    │ (cumulative)            │ (windowed - in memory)
    │                         │
    ├──► LiveMetricDataStore  │   (for real-time UI/gRPC queries)
    │    (reduced capacity)   │
    │                         │
    └────────┬────────────────┘
             │
             ▼
      Collectors (per iteration)
      - Read from aggregators directly
      - Cumulative: GetCumulativeData() (no reset)
      - Windowed: GetWindowDataAndReset() (resets)
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

## Real-Time Query Services

Services that need instant access to metrics use `ILiveMetricDataStore`:
- `MetricsUiService` - TUI display
- `MetricsQueryGrpcService` - Worker-to-master sync
- `ThroughputMetricAggregator` - Cross-metric dependency (reads ResponseCode data)

These services continue to work with immediate updates from cumulative aggregators.

## Design Rationale

### Why Two Stores?

1. **LiveMetricDataStore** (formerly MetricDataStoreService):
   - Provides real-time access to the latest metrics
   - Used by UI, gRPC queries, and cross-metric dependencies
   - Reduced capacity (256 per metric type) - only needs recent history
   - Updated immediately on every aggregator update

2. **WindowedMetricDataStore**:
   - Stores windowed snapshots for historical viewing
   - Only updated on window close events

### Why Collectors Read from Aggregators Directly?

Both `WindowedIterationMetricsCollector` and `CumulativeIterationMetricsCollector` now follow the same pattern:
- They receive aggregator references via property injection
- They call aggregator methods (`GetWindowDataAndReset()` or `GetCumulativeData()`) on coordinator events
- This ensures:
  - Consistent architecture
  - No stale data issues
  - Clear separation of concerns
