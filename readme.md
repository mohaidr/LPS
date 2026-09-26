# 🚀 Introduction

Welcome to the **LPS Tool** – your ultimate companion for **Load**, **Performance**, and **Stress** testing!

🛠️ The **LPS Tool** (Load, Performance, and Stress Testing Command Tool) is a flexible framework for testing your web application's performance under simulated load.

### 🌟 Key Highlights
- 🔁 Built on [**Rounds**](https://github.com/mohaidr/lps-docs/blob/main/concepts/1.Rounds.md) and [**Iterations**](https://github.com/mohaidr/lps-docs/blob/main/concepts/2.Iterations.md) for structured testing
- 🎛️ Offers flexible [**Iteration Modes**](https://github.com/mohaidr/lps-docs/blob/main/concepts/3.Iteration_Modes.md) to simulate real-world traffic
- 📊 Helps evaluate system **scalability**, **endurance**, and **resilience**
- ⚙️ Empowers developers and QA engineers with powerful testing scenarios

---

# 💻 Installation Guide

🧭 **LPS Tool is cross-platform** – it works on **Windows**, **Linux**, and **macOS**!

## 🛠️ Quick Install (Recommended)

You can now install the **LPS Tool** directly from **NuGet** as a global .NET CLI tool:

```bash
dotnet tool install --global lps
```

✅ **Requirements:**  
Make sure you have [.NET 8 SDK or Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed on your machine.

After installation, you can run LPS from anywhere using:

```bash
lps --version
```

---

✨ **That’s it!** You’re ready to start load testing with **LPS Tool**.

---

## Local Web Workspace (Source Build)

Build the dashboard before the .NET application. From the repository root in PowerShell:

```powershell
Push-Location lps-dashboard
npm.cmd install --legacy-peer-deps
npm.cmd run build
Pop-Location
dotnet build LPS/LPS.csproj
dotnet LPS/bin/Debug/net8.0/LPS.dll ui
```

The command starts a background host, opens http://127.0.0.1:8010/workspace in your default browser
once ready, and returns your terminal. Running `lps ui` again reuses the existing host.
Use `lps ui --open-browser false` to start without opening a browser, and `lps ui stop` to shut down.
The first version supports saved test plans, headers and raw bodies,
flat or staged load profiles, arrival delays, sequential users, request-count or duration workloads,
failure rules, termination rules with grace periods, iteration/request SkipIf conditions, graceful stop,
run history, retained metrics/logs, snapshots for reruns, and JSON export. One run can be active at a time.
The existing live dashboards remain available while a runner is active. The editor supports multiple
rounds and multiple iterations per round, with selection, naming, duplication, removal, and ordering.
Each iteration retains its own request, workload limits, rules, and conditions; stages and user settings
belong to its round. HTTP version choices are 1.1 and 2.0. Validation selects the affected round and
iteration. Shared iterations, variables, environments, and advanced iteration modes remain preserved
without editing their request structure.

The Editor shows the current run in cumulative endpoint tables grouped by round, with expandable
response codes, latency percentiles, transfer statistics, and request counts. Host tables remain
available after completion and in History using retained cumulative host snapshots. Live dashboard
links are available while the runner is active. Older runs that did not save host snapshots cannot
restore their host results. Windowed snapshots are excluded from results and downloads.
The History tab at `/workspace/history` provides searchable, status-filtered runs
with individual URLs at `/workspace/history/<run-id>`. Switching between Editor and History preserves
the draft. Each history entry includes results, logs, download, and restore-snapshot actions.

Live host and iteration charts mark actual cooling intervals with compact `C` flags:
amber for watchdog cooling and cyan for batch cooldowns. These remain visible while requests are in flight or other clients
are sending traffic. Batch pauses are recorded for CB, CRB, and DCB modes; overlapping pauses are
merged by source. Watchdog flags follow sampled Hot/Cooling states for each host. Both sources
are delivered in the next reporting window, including when they overlap. The `Flags On` / `Flags Off`
button beside `Points` shows or hides both sources' flags and shading across all tabs
without affecting metrics collection or export. Flags are shown by default.
The telemetry requires a newly started runner using the updated binaries; older runs cannot
reconstruct these intervals. Cooling tracking is local to the runner process; worker cooling
events are not currently forwarded to the master in distributed runs.

Live windowed charts also shade contiguous zero-activity periods as `Idle`.
Request rates use the current reporting window rather than a whole-run average. Idle windows show
zero requests and transfer, while latency remains a gap when there are no timing samples.
Idle shading alone does not imply cooling. Cumulative statistics are unchanged.
For CLI runs with InfluxDB enabled, `windowed_requests` includes `is_idle` (boolean),
`window_duration_ms`, and `requests_per_second`. The point timestamp is the window end; subtract
the duration to recover its start. Empty latency windows do not export fabricated zero timings.
`windowed_cooling` exports iteration intervals with a `source` tag (`Watchdog` or `BatchCooldown`),
integer `start_ns` and `end_ns` fields, and `duration_ms`. Its point timestamp is the interval end.
Intervals crossing reporting windows are split at those boundaries and can be joined by source.
Workspace-managed runs continue to disable InfluxDB output.

Stages replace the flat user count. Each stage has its own user count, arrival delay, and pause before
starting; sequential execution ignores arrival delays. Failure and termination rules support metric
thresholds, ranges, status-code filters, and custom expressions. Metric and expression conditions are
independent OR triggers; any rule can trigger. Rate thresholds appear as percentages in the editor
and remain ratios in exported plans. Custom metric syntax and placeholders are preserved.
SkipIf controls workload execution; the runner's warm-up may still contact the target origin.

The background host keeps running when the launching terminal closes. `lps ui stop` requests graceful
cancellation of any active test and waits for shutdown and final results. Use `lps ui stop --port 8011`
to stop a host on another port. Each test uses a separate local LPS process; ordinary CLI commands are
unchanged. `--port 8011` changes the UI port, `--webroot <directory>` selects a dashboard build,
and `--data-directory <directory>` selects persistent storage. A running host with a different explicit
data directory, or an unrelated service on the requested port, is not replaced or stopped.
By default, storage is under the OS local-application-data directory at `LPS/Workspace`.
Host diagnostics are appended to `ui-<port>.log` in that directory; routine HTTP request logs are
suppressed in background mode. `lps ui --foreground true` keeps the host in the terminal for debugging.
This is a per-user process, not an installed OS service; it does not automatically start at login.

Plans, run snapshots, and per-run settings are stored locally in plain text, including any supplied
credentials. Protect this directory and exported files. Recognized sensitive header values are
redacted from captured console output, but URLs, bodies, and engine-generated artifacts may contain
sensitive data. Run only trusted plans against targets you are authorized to test. This local-only
host is not an authenticated cloud service and must not be published through a proxy or tunnel.

OpenAPI is available at `/swagger` (set the workspace header to `1` using Authorize).
API examples are in [workspace.http](LPS/UI.Core/Web/workspace.http).
The dashboard remains a separate Git repository. Cloud accounts, collaboration, and the full
advanced plan editor are not part of this first version.

# ⚡ Quick Test Examples

### 1️⃣ Ramp up 1000 clients gradually
```bash
lps --url https://www.example.com --numberofclients 1000 --arrivaldelay 100
```
📎 **Starts 1000 clients gradually** with a 100 ms delay between client arrivals

---

### 2️⃣ Simple GET Request
```bash
lps --url https://www.example.com -rc 1000
```
📎 **Sends 1000 GET requests** to the specified URL

---

### 3️⃣ POST Request with Inline Payload
```bash
lps --url https://www.example.com -rc 1000 --httpmethod "POST" --payload "Inline Payload"
```
📎 **Sends 1000 POST requests** with a plain text payload

---

### 4️⃣ POST Request with File Payload
```bash
lps --url https://www.example.com -rc 1000 --httpmethod "POST" --payload "Path:C:\Users\User\Desktop\LPS\urnice.json"
```
📎 **Sends 1000 POST requests** using a JSON file as payload

---

### 5️⃣ POST Request with Payload URL
```bash
lps --url https://www.example.com -rc 1000 --httpmethod "POST" --payload "URL:https://www.example.com/payload"
```
📎 **Sends 1000 POST requests** where the payload is fetched from a URL

---

# 🌐 Distributed Load Testing

🌍 Distributed testing is supported starting from **v2.0.2_preview**.

📖 Learn more in the [Distributed Load Testing Article](https://github.com/mohaidr/lps-docs/blob/main/articles/9.DistributedLoadTesting.md)

---

# 📚 LPS Documentation

Explore full docs in the [📖 LPS Docs Repo](https://github.com/mohaidr/lps-docs/tree/main)

### Key Sections:
- 🧾 [Commands](https://github.com/mohaidr/lps-docs/blob/main/articles/1.Commands.md)
- 📄 [Articles](https://github.com/mohaidr/lps-docs/tree/main/articles)
- 🧠 [Concepts](https://github.com/mohaidr/lps-docs/tree/main/concepts)
- 💡 [Examples](https://github.com/mohaidr/lps-docs/tree/main/examples)
- 🔄 [Migration Guide](https://github.com/mohaidr/lps-docs/blob/main/articles/10.MigrationGuide.md) - Upgrading from v3.0.2.5 to v3.0.2.6+


# 🚨 Important Notice

> **⚠️ Warning:** This documentation applies **only** to **version 2.0_Preview** and above of the **LPS Tool**.
> For earlier versions, please visit the [`readme.md`](https://github.com/mohaidr/lps/tree/main/Version) in each version's directory.
> 
> ⚠️ **Note:** Version **2.x is NOT backward compatible** with 1.x.

> **🔄 Breaking Change in v3.0.2.6:** The `failureCriteria` and old `terminationRules` formats have been **permanently removed**. The last version supporting the old format was **v3.0.2.5**. See the [Migration Guide](https://github.com/mohaidr/lps-docs/blob/main/articles/10.MigrationGuide.md) for details on the new `failureRules` and `terminationRules` inline metric expression format.

---

