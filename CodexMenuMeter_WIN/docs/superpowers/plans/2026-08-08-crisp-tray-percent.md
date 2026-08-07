# Crisp Tray Percent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Windows tray quota crisp at native taskbar sizes and add a clear lower-right `%` without sacrificing the large quota number.

**Architecture:** Keep `NotifyIcon`, but replace GDI+ vector outlines with per-size hinted raster drawing. Support 16, 20, 24, 32, 40, 48, and 64 pixel frames and actively choose the closest frame for the current taskbar DPI before creating the HICON. A pure layout model reserves non-overlapping rectangles for the optional gray dot, main number, and lower-right percent mark.

**Tech Stack:** C# 5, .NET Framework 4.8, WinForms `TextRenderer`, System.Drawing, existing PowerShell build.

## Global Constraints

- Keep all outputs under `CodexMenuMeter_WIN` and add no dependency.
- Keep the number visually dominant and `%` at the lower right.
- Unknown quota remains `--` without `%`.
- Optional task dot remains gray, defaults off, and never implies task state.
- Preserve Per-Monitor V2, quota protocol, process detection, menu, and settings behavior.

---

### Task 1: Multi-size selection and three-part layout

**Files:**
- Modify: `CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs`
- Modify: `CodexMenuMeter_WIN/src/TrayApplicationContext.cs`

**Interfaces:**
- Produces: `TrayDpi.SupportedSizes`, `TrayDpi.ClosestSupportedSize(int)`, and `TrayLayout.PercentBounds`.
- Consumes: existing `TrayLayout.Calculate(int, bool)`.

- [x] **Step 1: Write failing tests**

```csharp
AssertEqual("16,20,24,32,40,48,64", string.Join(",", TrayDpi.SupportedSizes), "supported icon frames");
AssertEqual(16, TrayDpi.ClosestSupportedSize(17), "17px selects 16px frame");
AssertEqual(24, TrayDpi.ClosestSupportedSize(23), "23px selects 24px frame");
AssertEqual(40, TrayDpi.ClosestSupportedSize(36), "tie selects larger frame");

TrayLayout percent = TrayLayout.Calculate(16, false);
AssertTrue(percent.PercentBounds.Width >= 6, "percent remains readable at 16px");
AssertFalse(percent.NumberBounds.IntersectsWith(percent.PercentBounds), "number avoids percent");
AssertTrue(Contains(new Rectangle(0, 0, 16, 16), percent.PercentBounds), "percent fits icon");
```

Update the dotted-layout test to require dot, number, and percent rectangles to remain pairwise non-overlapping and inside the icon.

- [x] **Step 2: Run RED**

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test`.

Expected: compilation fails for `SupportedSizes`, `ClosestSupportedSize`, and `PercentBounds`.

- [x] **Step 3: Implement the layout**

Add `PercentBounds` to `TrayLayout`. For frame size `size`:

```csharp
int percent = Math.Max(6, size * 3 / 8);
Rectangle percentBounds = new Rectangle(size - percent, size - percent, percent, percent);
int topHeight = size - percent;
int dot = showDot ? Math.Max(4, size * 7 / 24) : 0;
Rectangle dotBounds = showDot ? new Rectangle(0, 0, dot, dot) : Rectangle.Empty;
Rectangle numberBounds = new Rectangle(showDot ? dot + 1 : 0, 0,
    size - (showDot ? dot + 1 : 0), topHeight);
```

Add the ordered frame list and choose the frame with the smallest absolute distance, preferring the larger frame on ties.

- [x] **Step 4: Run GREEN**

Run the same test command. Expected: all tests pass.

- [x] **Step 5: Commit**

```powershell
git add -- CodexMenuMeter_WIN/src/TrayApplicationContext.cs CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs CodexMenuMeter_WIN/docs/superpowers/plans/2026-08-08-crisp-tray-percent.md
git commit -m "feat: add crisp tray percent layout"
```

### Task 2: Hinted text renderer and final delivery

**Files:**
- Modify: `CodexMenuMeter_WIN/src/TrayApplicationContext.cs`
- Modify: `CodexMenuMeter_WIN/README.md`
- Modify: `CodexMenuMeter_WIN/codex_windows_tray_meter_requirements.md`
- Modify: `CodexMenuMeter_WIN/docs/ARCHITECTURE.md`
- Modify: `CodexMenuMeter_WIN/docs/TESTING.md`
- Modify: `CodexMenuMeter_WIN/docs/superpowers/specs/2026-08-08-windows-tray-readability-design.md`
- Modify: `CodexMenuMeter_WIN/dist/CodexMenuMeter.exe`

**Interfaces:**
- Consumes: `TrayLayout` and `TrayDpi.ClosestSupportedSize`.
- Produces: `TrayIconRenderer.Render(string, bool)` using one DPI-matched crisp HICON.

- [x] **Step 1: Replace vector outlines**

Delete `GraphicsPath.AddString`, `Matrix`, and outline drawing. Render each label with `TextRenderer.MeasureText` and `TextRenderer.DrawText`, `TextFormatFlags.NoPadding | SingleLine | HorizontalCenter | VerticalCenter | NoPrefix`, decreasing a bold `Arial Narrow` pixel font until it fits its rectangle.

Draw the number in `NumberBounds`. When text is not `--`, draw `%` in `PercentBounds`; if no font fits the 16px percent rectangle, draw an integer-aligned percent mark with two filled dots and a one-pixel diagonal.

- [x] **Step 2: Select the DPI frame**

Keep `GetDpiForWindow(Shell_TrayWnd)` and `GetSystemMetricsForDpi`, then normalize the requested size through `ClosestSupportedSize`. Render directly at that selected size and create the HICON without a second resize.

- [x] **Step 3: Verify tests and visual probe**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Clean
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Generate 16, 20, 24, 32, 40, 48, and 64 pixel probe images with the production renderer. At 16px, require readable `39`, recognizable lower-right `%`, no gray outline, and no overlap. Check the optional gray dot separately.

- [x] **Step 4: Update documentation**

Document the restored lower-right `%`, active DPI-frame selection, hinted text, and removal of vector outlines. Correct the design spec statement so the application, not Shell, selects the frame passed through `NotifyIcon`.

- [x] **Step 5: Copy and verify final binary**

Stop only the running `dist\CodexMenuMeter.exe`, wait for exit, copy the verified build, relaunch it, and require build/dist SHA-256 equality. Run `git diff --check`.

- [x] **Step 6: Commit**

```powershell
git add -- CodexMenuMeter_WIN
git commit -m "fix: sharpen tray quota and percent"
```
