# Bold Sharp Tray Number Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the no-percent Windows tray quota number bolder while preserving sharp monochrome native-DPI pixels.

**Architecture:** Keep the existing renderer and full-size number layout. Change only the number font weight from Regular to Bold; retain `SingleBitPerPixelGridFit` and validate both pixel density and absence of colored fringes.

**Tech Stack:** C# 5, .NET Framework 4.8, System.Drawing, existing PowerShell build.

## Global Constraints

- Do not restore `%` in the tray icon.
- Do not add outlines, ClearType, antialiasing, shadows, scaling, or dependencies.
- Keep real App Server quota behavior and all files under `CodexMenuMeter_WIN`.

---

### Task 1: Increase number weight and deliver

**Files:**
- Modify: `CodexMenuMeter_WIN/tests/CodexMenuMeterTests.cs`
- Modify: `CodexMenuMeter_WIN/src/TrayApplicationContext.cs`
- Modify: `CodexMenuMeter_WIN/README.md`
- Modify: `CodexMenuMeter_WIN/codex_windows_tray_meter_requirements.md`
- Modify: `CodexMenuMeter_WIN/docs/ARCHITECTURE.md`
- Modify: `CodexMenuMeter_WIN/docs/TESTING.md`
- Modify: `CodexMenuMeter_WIN/dist/CodexMenuMeter.exe`

**Interfaces:**
- Consumes: `TrayIconRenderer.Render(string, bool, int)`.
- Produces: a bold 16px `39` with at least 45 visible pixels and no colored pixels.

- [x] **Step 1: Write the failing test**

Add `CountVisiblePixels(Bitmap)` and assert that production rendering of `39` at 16px contains at least 45 visible pixels. Retain the existing `HasColoredPixel` assertion.

- [x] **Step 2: Run RED**

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test`.

Expected: FAIL because the current Regular glyph has 33 visible pixels.

- [x] **Step 3: Implement minimal production change**

Change `new Font("Arial Narrow", pixels, FontStyle.Regular, GraphicsUnit.Pixel)` to `FontStyle.Bold`; make no other renderer change.

- [x] **Step 4: Verify and deliver**

Run clean tests and build, generate the 16px production pixel probe, update current documentation from regular/thin to bold, replace only `dist\CodexMenuMeter.exe`, require build/dist SHA-256 equality, restart it, run `git diff --check`, and commit as `fix: bolden sharp tray number`.
