# Thin Number Without Percent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render a thinner, maximum-size quota number in the Windows tray without a percent mark.

**Architecture:** Keep the existing DPI selection and production renderer. Remove the percent layout and drawing path, give the number the full available icon rectangle, and change the hinted font from bold to regular.

**Tech Stack:** C# 5, .NET Framework 4.8, System.Drawing `SingleBitPerPixelGridFit`, existing PowerShell build.

## Global Constraints

- Keep all outputs under `CodexMenuMeter_WIN` and add no dependency.
- Keep hover and menu quota text as percentages.
- Unknown quota remains `--`; optional gray status dot remains default-off.
- Use only real App Server data for live validation.

---

### Task 1: Remove the tray percent and thin the number

**Files:**
- Modify: `CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs`
- Modify: `CodexMenuMeter_WIN/src/TrayApplicationContext.cs`
- Modify: `CodexMenuMeter_WIN/README.md`
- Modify: `CodexMenuMeter_WIN/codex_windows_tray_meter_requirements.md`
- Modify: `CodexMenuMeter_WIN/docs/ARCHITECTURE.md`
- Modify: `CodexMenuMeter_WIN/docs/TESTING.md`
- Modify: `CodexMenuMeter_WIN/dist/CodexMenuMeter.exe`

**Interfaces:**
- Consumes: `TrayLayout.Calculate(int, bool)` and `TrayIconRenderer.Render(string, bool, int)`.
- Produces: a full-size number rectangle and a tray bitmap containing no percent pixels.

- [x] **Step 1: Write the failing test**

Require the plain number bounds to equal the full icon, the dotted number bounds to use all space below the dot, and remove every assertion for `PercentBounds`. Compare a rendered `39` bitmap with the number bounds only.

- [x] **Step 2: Run RED**

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test`.

Expected: layout assertions fail because production code still reserves percent space.

- [x] **Step 3: Implement the minimal change**

Remove `PercentBounds` and `DrawPercent`. Make the plain number bounds `new Rectangle(0, 0, size, size)` and the dotted number bounds `new Rectangle(0, dot + 1, size, size - dot - 1)`. Change `FontStyle.Bold` to `FontStyle.Regular` and draw with `TextRenderingHint.SingleBitPerPixelGridFit` to avoid ClearType color fringes on the transparent HICON.

- [x] **Step 4: Update current documentation**

Document that the tray shows a thin integer without `%`; keep `%` in hover, menu, and live-test examples.

- [x] **Step 5: Verify and deliver**

Run a clean test/build, require `PASS 53 tests` or higher, copy the matching SHA-256 binary to `dist`, restart only that executable, run `git diff --check`, and commit as `fix: thin tray number and remove percent`.
