# Windows Tray Readability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the small `%` tray badge with the largest possible native-DPI quota number, add the macOS-style optional gray status-dot reservation, remove unavailable task text, and render WinForms menus sharply on Windows 11.

**Architecture:** Keep the dependency-free .NET Framework 4.8 WinForms executable. Pure formatting and icon geometry remain testable helpers in `TrayApplicationContext.cs`; a small registry-backed preference and WinForms settings dialog reuse the existing HKCU settings key. An embedded Per-Monitor V2 manifest and taskbar-DPI lookup prevent Windows bitmap scaling.

**Tech Stack:** C# 5, .NET Framework 4.8, WinForms, System.Drawing, Win32 DPI APIs, built-in `csc.exe`, PowerShell.

## Global Constraints

- Keep every source, test, document, and binary under `CodexMenuMeter_WIN`.
- Add no SDK, NuGet package, UI framework, or image dependency.
- Show only the integer quota (`39`) or unavailable marker (`--`); never draw `%`.
- The optional task-status dot defaults off and can only be gray in this release.
- Do not expose unavailable task state in the menu or infer desktop task activity.
- Preserve the official App Server quota source and the rule that the tray icon exists only while the official Codex desktop app runs.

---

### Task 1: Number formatting, preference parsing, and icon geometry

**Files:**
- Modify: `CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs`
- Modify: `CodexMenuMeter_WIN/src/TrayApplicationContext.cs`

**Interfaces:**
- Produces: `TrayText.Format(int?)`, `MeterSettings.ParseShowTaskStatusDot(object)`, and `TrayLayout.Calculate(int, bool)`.
- Consumes: existing `Rectangle` and registry settings key.

- [x] **Step 1: Write failing tests**

Add assertions:

```csharp
AssertEqual("39", TrayText.Format(39), "quota omits percent sign");
AssertEqual("--", TrayText.Format(null), "unknown quota omits percent sign");
AssertFalse(MeterSettings.ParseShowTaskStatusDot(null), "status dot defaults off");
AssertTrue(MeterSettings.ParseShowTaskStatusDot(1), "status dot persisted on");

TrayLayout plain = TrayLayout.Calculate(24, false);
AssertEqual(Rectangle.Empty, plain.DotBounds, "hidden dot has no bounds");
AssertTrue(Contains(new Rectangle(0, 0, 24, 24), plain.NumberBounds), "plain number fits icon");
TrayLayout dotted = TrayLayout.Calculate(24, true);
AssertTrue(dotted.DotBounds.Width > 0, "shown dot reserves pixels");
AssertTrue(Contains(new Rectangle(0, 0, 24, 24), dotted.NumberBounds), "dotted number fits icon");
AssertFalse(dotted.DotBounds.IntersectsWith(dotted.NumberBounds), "dot and number do not overlap");
```

Add a local `Contains(Rectangle outer, Rectangle inner)` test helper.

- [x] **Step 2: Run tests and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Expected: failures for old `39%`/`--%` output and missing `MeterSettings`/`TrayLayout`.

- [x] **Step 3: Implement minimal pure behavior**

Change `TrayText.Format` to return the number without `%`. Add:

```csharp
internal sealed class TrayLayout
{
    public readonly Rectangle DotBounds;
    public readonly Rectangle NumberBounds;

    private TrayLayout(Rectangle dotBounds, Rectangle numberBounds)
    {
        DotBounds = dotBounds;
        NumberBounds = numberBounds;
    }

    public static TrayLayout Calculate(int size, bool showDot)
    {
        int edge = Math.Max(1, size / 16);
        if (!showDot) return new TrayLayout(Rectangle.Empty,
            new Rectangle(edge, edge, size - edge * 2, size - edge * 2));
        int dot = Math.Max(4, size * 7 / 24);
        return new TrayLayout(new Rectangle(edge, edge, dot, dot),
            new Rectangle(dot + edge * 2, edge, size - dot - edge * 3, size - edge * 2));
    }
}

internal static class MeterSettings
{
    public static bool ParseShowTaskStatusDot(object value)
    {
        return value is int && (int)value != 0;
    }
}
```

- [x] **Step 4: Run tests and verify GREEN**

Run the same `-Test` command. Expected: all assertions pass.

- [x] **Step 5: Commit Task 1**

```powershell
git add -- CodexMenuMeter_WIN/src/TrayApplicationContext.cs CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs
git commit -m "feat: maximize tray quota number"
```

### Task 2: Native-DPI icon rendering and settings UI

**Files:**
- Create: `CodexMenuMeter_WIN/app.manifest`
- Modify: `CodexMenuMeter_WIN/src/TrayApplicationContext.cs`
- Modify: `CodexMenuMeter_WIN/build.ps1`

**Interfaces:**
- Consumes: `TrayLayout.Calculate(int, bool)` and `TrayText.Format(int?)`.
- Produces: `TrayDpi.IconSize()`, `MeterSettings.ShowTaskStatusDot`, `SettingsForm`, and `TrayIconRenderer.Render(string, bool)`.

- [x] **Step 1: Add a failing manifest/build check**

Add a PowerShell verification after GUI compilation:

```powershell
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'app.manifest'))) {
    throw 'Per-Monitor V2 manifest is missing.'
}
```

Run the production build. Expected: fail because `app.manifest` does not exist.

- [x] **Step 2: Add the Per-Monitor V2 manifest**

Create `app.manifest` with `dpiAware=true/pm` and `dpiAwareness=PerMonitorV2,PerMonitor`. Pass it to the GUI compiler:

```powershell
/win32manifest:(Join-Path $projectRoot 'app.manifest')
```

- [x] **Step 3: Implement exact-DPI rendering**

Add `TrayDpi` using `FindWindow("Shell_TrayWnd")`, `GetDpiForWindow`, and `GetSystemMetricsForDpi(SM_CXSMICON, dpi)`, with `SystemInformation.SmallIconSize.Width` fallback. Replace the fixed 64×64 renderer with a bitmap whose width and height equal `TrayDpi.IconSize()`.

Build the quota glyph with `GraphicsPath.AddString`, scale it into `TrayLayout.NumberBounds`, and draw a one-pixel contrasting outline plus foreground fill. Draw the optional gray ellipse in `DotBounds`. Do not draw a colored tile or `%`.

- [x] **Step 4: Implement registry preference and settings window**

Extend `MeterSettings`:

```csharp
public static bool ShowTaskStatusDot { get; set; }
```

Store `ShowTaskStatusDot` as DWORD under `HKCU\Software\CodexMenuMeter`. Add `SettingsForm` with `AutoScaleMode = AutoScaleMode.Dpi`, system message-box font, dot and startup checkboxes, explanatory label, Save, and Cancel. Save invokes a callback so the tray icon refreshes immediately.

- [x] **Step 5: Simplify the tray menu and state**

Delete the runtime `taskState`, `taskError`, task rows, aggregate tooltip text, and the unavailable task menu row. Set `menu.Font = SystemFonts.MenuFont`, add “设置…”, and call:

```csharp
    string text = TrayText.Format(quota == null ? (int?)null : quota.RemainingPercent);
    TrayIconRenderer.Render(text, MeterSettings.ShowTaskStatusDot)
```

- [x] **Step 6: Run tests and build**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Expected: tests pass and `build\CodexMenuMeter.exe` compiles with the manifest.

- [x] **Step 7: Commit Task 2**

```powershell
git add -- CodexMenuMeter_WIN/app.manifest CodexMenuMeter_WIN/build.ps1 CodexMenuMeter_WIN/src/TrayApplicationContext.cs
git commit -m "fix: render Windows tray UI at native DPI"
```

### Task 3: Documentation, executable, and Windows verification

**Files:**
- Modify: `CodexMenuMeter_WIN/README.md`
- Modify: `CodexMenuMeter_WIN/codex_windows_tray_meter_requirements.md`
- Modify: `CodexMenuMeter_WIN/docs/ARCHITECTURE.md`
- Modify: `CodexMenuMeter_WIN/docs/TESTING.md`
- Modify: `CodexMenuMeter_WIN/dist/CodexMenuMeter.exe`

**Interfaces:**
- Consumes: the verified GUI executable.
- Produces: current user documentation and final binary.

- [x] **Step 1: Update user documentation**

Document number-only display, `--`, optional gray status dot, removed task row, settings window, Per-Monitor V2 rendering, and the existing real-interface test boundary. Do not claim task status is available.

- [x] **Step 2: Run clean verification**

Stop only the test-launched `CodexMenuMeter.exe`, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Clean
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
git diff --check
```

Expected: clean build, all tests pass, and no whitespace errors.

- [x] **Step 3: Copy and launch the verified binary**

Copy `build\CodexMenuMeter.exe` to `dist\CodexMenuMeter.exe`, launch it while official Codex is running, and verify the process remains alive. Record the build/dist SHA-256 and require exact equality.

- [x] **Step 4: Inspect Windows UI**

Verify with real Windows UI that the icon shows a large number without `%`, toggling the setting shows/hides only a gray dot, the menu contains no task-status row, and menu/settings text is crisp at the current DPI. Do not substitute mocked quota data for the live quota value.

- [x] **Step 5: Commit Task 3**

```powershell
git add -- CodexMenuMeter_WIN
git commit -m "docs: finish Windows tray readability update"
```
