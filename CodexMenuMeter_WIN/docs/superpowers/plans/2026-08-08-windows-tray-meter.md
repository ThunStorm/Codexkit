# CodexMenuMeter Windows 11 Tray Meter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows 11 tray application that shows Codex quota as `69%`, appears only while the official Codex desktop app runs, and conditionally shows reliable task state.

**Architecture:** A dependency-free C# 5 WinForms executable uses one long-lived `codex app-server` child for quota and thread summaries. Pure domain and protocol logic stays separate from the tray lifecycle so a console test executable can verify it without UI automation.

**Tech Stack:** .NET Framework 4.8, C# 5, WinForms, System.Drawing, System.Web.Extensions, PowerShell build script, built-in `csc.exe`.

## Global Constraints

- All source, tests, scripts, binaries, and documentation remain under `CodexMenuMeter_WIN`.
- Do not install an SDK, NuGet package, Electron runtime, or other dependency.
- Detect only `ChatGPT.exe` processes whose executable path contains the `OpenAI.Codex_` package marker.
- Use only the official `codex app-server`; never read auth files, SQLite, session JSONL, prompts, commands, paths, diffs, output, or process memory.
- Prefer a 5-hour quota window (`240...360` minutes), then a weekly window (`9000...11000` minutes).
- Disable task colors and task rows unless a separate App Server can observe a real desktop thread entering and leaving `active`.
- Green means observed `active -> no active`, not successful completion.

---

### Task 1: Domain model and quota selection

**Files:**
- Create: `CodexMenuMeter_WIN/src/Domain.cs`
- Create: `CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs`
- Create: `CodexMenuMeter_WIN/build.ps1`
- Include in commit: `CodexMenuMeter_WIN/docs/superpowers/plans/2026-08-08-windows-tray-meter.md`

**Interfaces:**
- Produces: `QuotaWindow`, `QuotaSelector.Remaining(double)`, `QuotaSelector.Select(IEnumerable<QuotaWindow>)`, `TaskAggregate`, `TaskTracker.Update(IEnumerable<TaskSummary>, bool)`.
- Consumes: only `System`, `System.Collections.Generic`, and `System.Linq`.

- [x] **Step 1: Write the failing quota and task-state tests**

Create a console test runner with named assertions covering:

```csharp
AssertEqual(69, QuotaSelector.Remaining(31), "remaining percent");
AssertEqual(null, QuotaSelector.Remaining(101), "invalid percent");
AssertEqual(QuotaKind.FiveHour, QuotaSelector.Select(new[] {
    new QuotaWindow(QuotaKind.Weekly, 29, 10080, null),
    new QuotaWindow(QuotaKind.FiveHour, 31, 300, null)
}).Kind, "five hour wins");

var tracker = new TaskTracker();
AssertEqual(TaskColor.Yellow, tracker.Update(new[] {
    new TaskSummary("1", "Build", TaskState.Active, null, DateTime.UtcNow)
}, true).Color, "active is yellow");
AssertEqual(TaskColor.Green, tracker.Update(new TaskSummary[0], true).Color,
    "observed active then idle is green");
AssertEqual(TaskColor.Gray, tracker.Update(new TaskSummary[0], false).Color,
    "lost source is gray");
```

The runner exits `0` only when every assertion passes and prints `PASS <count> tests`.

- [x] **Step 2: Run the test build and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Expected: compilation fails because `src\Domain.cs` and its types do not exist.

- [x] **Step 3: Implement the minimal domain logic**

Create these exact types:

```csharp
enum QuotaKind { FiveHour, Weekly, Other }
sealed class QuotaWindow {
    public readonly QuotaKind Kind;
    public readonly double UsedPercent;
    public readonly int DurationMinutes;
    public readonly DateTime? ResetsAt;
    public int RemainingPercent { get { return QuotaSelector.Remaining(UsedPercent).Value; } }
}
static class QuotaSelector {
    public static QuotaKind KindFor(int minutes);
    public static int? Remaining(double usedPercent);
    public static QuotaWindow Select(IEnumerable<QuotaWindow> windows);
}
enum TaskState { Active, WaitingForApproval, WaitingForInput, SystemError, Idle }
enum TaskColor { Gray, Yellow, Green, Red }
sealed class TaskSummary { /* id, name, state, observed start, updated time */ }
sealed class TaskAggregate { /* color and at most five active task rows */ }
sealed class TaskTracker {
    public TaskAggregate Update(IEnumerable<TaskSummary> tasks, bool sourceAvailable);
    public void Reset();
}
```

Rules: reject NaN, infinity, and values outside `0...100`; red beats yellow; cap rows at five; use `未命名任务` for an empty name; green only after this tracker instance previously observed an active task.

- [x] **Step 4: Compile and verify GREEN**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Expected: `PASS` with zero failures.

- [x] **Step 5: Commit Task 1**

```powershell
git add -- CodexMenuMeter_WIN/src/Domain.cs CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs CodexMenuMeter_WIN/build.ps1 CodexMenuMeter_WIN/docs/superpowers/plans/2026-08-08-windows-tray-meter.md
git commit -m "feat: add quota and task domain rules"
```

### Task 2: JSONL App Server client and response parsing

**Files:**
- Create: `CodexMenuMeter_WIN/src/AppServerClient.cs`
- Modify: `CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs`
- Modify: `CodexMenuMeter_WIN/build.ps1`

**Interfaces:**
- Consumes: `QuotaWindow`, `QuotaSelector`, and `TaskSummary` from Task 1.
- Produces: `AppServerResponseParser.ParseQuota(string)`, `AppServerResponseParser.ParseTasks(string)`, and `AppServerClient.RequestAsync(string, object, int)`.

- [x] **Step 1: Add failing parser tests**

Add assertions for unknown notifications, numeric `resetsAt`, signed-in accounts where `requiresOpenaiAuth` is true, five-hour selection, and thread statuses. `StreamReader.ReadLineAsync()` handles stdout fragmentation, so no duplicate line buffer is added:

```csharp
var tasks = AppServerResponseParser.ParseTasks(
    "{\"result\":{\"data\":[" +
    "{\"id\":\"a\",\"name\":\"Fix build\",\"updatedAt\":1780000000," +
    "\"status\":{\"type\":\"active\",\"activeFlags\":[\"waitingOnApproval\"]}}]}}");
AssertEqual(TaskState.WaitingForApproval, tasks[0].State, "approval maps red");
```

- [x] **Step 2: Run tests and verify RED**

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test`.

Expected: compilation fails because `AppServerResponseParser` does not exist.

- [x] **Step 3: Implement JSONL and protocol code**

Use `JavaScriptSerializer` and dictionaries; do not add a JSON dependency. `AppServerClient` must:

```csharp
Task StartAsync();
Task<string> RequestAsync(string method, object parameters, int timeoutMilliseconds);
Task InitializeAsync();
Task<QuotaWindow> ReadQuotaAsync();
Task<IList<TaskSummary>> ReadTasksAsync();
void Dispose();
```

Start `codex app-server --listen stdio://`, redirect stdin/stdout/stderr, remove `OPENAI_API_KEY`, `CODEX_API_KEY`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN` from the child environment, send newline-delimited JSON, and route responses by integer `id`. Ignore unknown notifications. `ReadQuotaAsync` calls `account/read` before `account/rateLimits/read`; `ReadTasksAsync` calls `thread/list` with `sourceKinds: ["appServer"]`, newest-first ordering, and a limit of 100, then returns only active/error summaries without reading turns or items. This excludes terminal CLI and VS Code sessions.

- [x] **Step 4: Run all tests and verify GREEN**

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test`.

Expected: all parser and domain tests pass.

- [x] **Step 5: Commit Task 2**

```powershell
git add -- CodexMenuMeter_WIN/src/AppServerClient.cs CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs CodexMenuMeter_WIN/build.ps1
git commit -m "feat: add Codex app-server client"
```

### Task 3: Process monitor, tray UI, and login startup

**Files:**
- Create: `CodexMenuMeter_WIN/src/Program.cs`
- Create: `CodexMenuMeter_WIN/src/TrayApplicationContext.cs`
- Modify: `CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs`
- Modify: `CodexMenuMeter_WIN/build.ps1`

**Interfaces:**
- Consumes: domain types and `AppServerClient` from Tasks 1-2.
- Produces: `CodexProcessMonitor.IsOfficial(string, string)`, `TrayIconRenderer.Render(string, TaskColor)`, `StartupRegistration.Enabled`, and the `CodexMenuMeter.exe` GUI entry point.

- [x] **Step 1: Add failing process and display tests**

```csharp
AssertTrue(CodexProcessMonitor.IsOfficial("ChatGPT",
    @"C:\Program Files\WindowsApps\OpenAI.Codex_26.803.5235.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe"),
    "official desktop process");
AssertFalse(CodexProcessMonitor.IsOfficial("codex",
    @"C:\Program Files\WindowsApps\OpenAI.Codex_26.803.5235.0_x64__2p2nqsd0c76g0\app\resources\codex.exe"),
    "CLI is excluded");
AssertEqual("69%", TrayText.Format(69), "visible percent");
AssertEqual("--%", TrayText.Format(null), "unknown percent");
```

- [x] **Step 2: Run tests and verify RED**

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test`.

Expected: compilation fails because the process and display helpers do not exist.

- [x] **Step 3: Implement the WinForms application**

`Program.Main` runs `Application.Run(new TrayApplicationContext())` with visual styles enabled. `TrayApplicationContext` must:

- poll processes every 2 seconds;
- create `NotifyIcon` and App Server only while an official process exists;
- refresh quota every 60 seconds and tasks every 2 seconds without overlapping refreshes;
- render multi-size icons with a large number and small `%` using `System.Drawing`;
- show quota, reset/update time, aggregate task state, and up to five task rows in the click menu;
- offer Open Codex, Refresh, startup toggle, and Exit;
- clear stale quota after five minutes;
- turn task state gray and clear rows on task query failure;
- dispose the icon, timer, generated icons, and child process on shutdown.

`StartupRegistration` reads/writes only `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. First run enables the value; the menu toggles it. No administrator elevation is requested.

- [x] **Step 4: Run tests and build the GUI executable**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Expected: tests pass and `build\CodexMenuMeter.exe` exists as a Windows GUI executable.

- [x] **Step 5: Commit Task 3**

```powershell
git add -- CodexMenuMeter_WIN/src/Program.cs CodexMenuMeter_WIN/src/TrayApplicationContext.cs CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs CodexMenuMeter_WIN/build.ps1
git commit -m "feat: add Windows Codex tray meter"
```

### Task 4: Live feasibility gate and Windows documentation

**Files:**
- Rename: `CodexMenuMeter_WIN/codex_macos_menu_bar_usage_status_plan.md` -> `CodexMenuMeter_WIN/codex_windows_tray_meter_requirements.md`
- Delete: `CodexMenuMeter_WIN/plan_preview.md`
- Modify: `CodexMenuMeter_WIN/README.md`
- Modify: `CodexMenuMeter_WIN/docs/ARCHITECTURE.md`
- Modify: `CodexMenuMeter_WIN/docs/PROTOCOL.md`
- Modify: `CodexMenuMeter_WIN/docs/TESTING.md`
- Modify: `CodexMenuMeter_WIN/docs/TASK_STATUS.md`
- Modify: `CodexMenuMeter_WIN/docs/feasibility.md`
- Create: `CodexMenuMeter_WIN/dist/CodexMenuMeter.exe`

**Interfaces:**
- Consumes: the complete app and tests.
- Produces: current Windows documentation, a verified build, and a recorded task-status feasibility result.

- [x] **Step 1: Run the live task-status probe**

With the official Codex desktop app running, launch the meter and observe one real task through start and finish. Record only thread ID, sanitized official name, and state transitions. Pass only if the independent App Server reports the desktop task as `active` and later non-active. If it fails, keep task status gray and omit task rows; do not inspect SQLite or JSONL.

- [x] **Step 2: Rewrite and rename documentation**

Document exact Windows build/run commands, first-run tray pinning, startup toggle, uninstall steps, protocol fields, status meanings, privacy boundary, test output, task probe result, and unsigned-binary SmartScreen limitation. Replace every macOS path, Swift command, AppKit term, and stale CLI version claim. Delete the redundant preview plan.

- [x] **Step 3: Run final verification**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Clean
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
Get-FileHash .\build\CodexMenuMeter.exe -Algorithm SHA256
rg -n "macOS|Swift|NSStatus|menu_meter/CodexMenuMeter|T[B]D|T[O]DO" README.md docs codex_windows_tray_meter_requirements.md
```

Expected: clean build; all tests pass; executable hash prints; stale-document scan has no unintended matches.

- [x] **Step 4: Copy the verified executable and inspect scope**

Copy `build\CodexMenuMeter.exe` to `dist\CodexMenuMeter.exe`, then run `git diff --check` and `git status --short`. Confirm every new artifact is inside `CodexMenuMeter_WIN` and unrelated user changes remain untouched.

- [x] **Step 5: Commit Task 4**

```powershell
git add -- CodexMenuMeter_WIN
git commit -m "docs: finish Windows tray meter delivery"
```
