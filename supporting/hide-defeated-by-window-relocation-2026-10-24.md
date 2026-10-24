# What defeats Drive's hide: measured, 2026-10-24

The report's section 3a says DriveFS allocates a console and then tries to hide its window, and that on this machine the hide reports success while the window stays visible. This page records how that was measured on 2026-10-24, what the measurements showed, and what the mechanism turned out to be. Short version: **the hide works. Something else shows the window again a few milliseconds later, and on this machine that something is PowerToys FancyZones moving the new window to the monitor the mouse cursor is on.**

Environment: Windows 11 Pro 10.0.26300, three 2560x1440 monitors at 125% with the primary in the middle, console host `conhost.exe` (default terminal "Let Windows decide"), Drive for desktop 132.0.0.0, PowerToys 0.101.2362.0 with FancyZones enabled.

## 1. Method

A window-event recorder (`for-powertoys/console-window-events.ps1`, a `SetWinEventHook` listener) logs every create, show, hide, move, title change and destroy of any `ConsoleWindowClass` window, with the owning process, a timestamp, the window rectangle, and the time since the owning process started. Drive's child was then started and killed repeatedly, with `Stop-Process` (so Drive keeps running) or with the window's X button, while the recorder watched. The complete event log is `console-window-events-2026-10-24.log` in this folder.

Three small test programs in `for-powertoys/` replace Drive with the minimum needed to reproduce each step (build instructions in that folder's README):

- `consoletest.cs`: a GUI-subsystem process that calls `AllocConsole`, `GetConsoleWindow`, `ShowWindow(SW_HIDE)`, exactly Drive's sequence, and samples the window's visibility afterwards.
- `consoletest2.cs`: the same, plus opening `CONOUT$` as stderr and writing log lines immediately, as Drive does.
- `consoletest3.cs`: a parent/child harness with Drive's process structure. The parent spawns the child with a zeroed `STARTUPINFO` and no creation flags (as Drive's parent does, measured from the child's PEB), optionally assigns it to a job object, waits for it to exit, and restarts it. The child does what `consoletest2` does, with an optional delay before the hide.

## 2. Drive's own windows: the hide lands, then a second show

Two full close-loops (14 children) with the recorder running. Times are milliseconds after the child process started.

| Child | conhost shows | Drive hides | shown again | Outcome |
|---|---|---|---|---|
| first child of parent A | +115 | +121 | no | hidden |
| 6 restart children | +95 to +199 | 5 to 13 ms after the show | 1 to 5 ms after the hide | all visible |
| first child of parent B | +110 | +120 | no | hidden |
| 6 restart children | +93 to +128 | 5 to 8 ms after the show | 1 to 5 ms after the hide | all visible |

So the hide is not late and does not fail. It lands, the window is hidden, and then the window is shown again. The only trace of the hide in Drive's own log is the absence of "Can't hide the console": the binary has no success message for it (report, section 3a).

A first child and a restart child have identical startup parameters (no `STARTF_USESHOWWINDOW`, show state 0, read from both PEBs), so the first/restart difference is not something the parent passes.

## 3. Minimal programs: the second show needs none of Drive's code

| Program | Runs | Result |
|---|---|---|
| `consoletest` (alloc, hide), fresh, back-to-back, and killed-then-restarted | 11 | 11 hidden |
| `consoletest2` (alloc, hide, open `CONOUT$`, write 50 or 2,000 lines) | 8 | 8 hidden |
| `consoletest3` harness, first child of each parent, job object on or off | 2 | 2 hidden |
| `consoletest3` harness, restart children, job object on or off | 6 | 6 visible |

The harness reproduces Drive's pattern without Drive. With the recorder's window rectangles added, the restart children's second show came with a **move of exactly one monitor width**: the window was created on the primary monitor, hidden, moved to another monitor, and shown there, all within about 20 ms.

## 4. The mouse cursor decides it

The harness was run with the cursor placed on each monitor (`SetCursorPos`), 3 children per run:

| Cursor on | Result |
|---|---|
| left monitor | 3 of 3 visible, moved to the left monitor |
| right monitor | 2 of 3 visible, moved to the right monitor; 1 hidden |
| primary monitor | 3 of 3 hidden, no move |

With the hide delayed until after the move (cursor on the left monitor): hide at 150 ms, 3 of 3 hidden; hide at 400 ms, 3 of 3 hidden; immediate hide as control, 2 of 2 visible. The move happens 10 to 20 ms after conhost's first show whether or not the window is hidden at that moment; it carries a show with it; a hide issued before it is undone, a hide issued after it sticks.

This explains the first-child/restart-child pattern in section 3 of the report: the cursor is on the primary monitor when you click the Start menu, and somewhere else when a self-restart fires or when you are clicking the X on a loop. It explains the 2026-10-21 window appearing on the left monitor, and the 2026-10-23 "it stopped happening" afternoon (cursor on the primary while typing).

## 5. Drive with one monitor, then with three

The two extra monitors were physically disconnected, then reconnected:

| Condition | Drive children | Result |
|---|---|---|
| three monitors, cursor on the left, closing with X | 7 | 7 visible, each moved to the left monitor after the hide |
| one monitor, kills and a fresh parent | 8 | 8 hidden, no move |

## 6. The relocator: PowerToys FancyZones

conhost does not do this. Its source (`window.cpp`, `windowproc.cpp`, `windowio.cpp`, `srvinit.cpp` in microsoft/terminal) shows the window once, in `ActivateAndShow`, and has no cursor-based or monitor-based relocation.

This machine runs PowerToys with FancyZones enabled and its option **"Move newly created windows to the current active monitor"** (`fancyzones_openWindowOnActiveMonitor`) on. That option does precisely what was measured: a new window appears, FancyZones moves it to the monitor the cursor is on, and the move shows it. Toggled with three monitors reconnected and the cursor on the left monitor:

| FancyZones state | Drive children | Result |
|---|---|---|
| option on | 6 | 6 visible, moved to the left monitor |
| option off | 5 | 5 hidden |
| option on again | 2 | 2 visible |
| option on, `GoogleDriveFS.exe` in FancyZones' excluded apps | 5 | 5 hidden |

## 7. What this means

**For Google.** Section 3a's "race against conhost" was the wrong picture. `ShowWindow(SW_HIDE)` immediately after `AllocConsole` works on a bare system. What it cannot survive is any tool that relocates or re-shows new windows afterwards, and such tools exist (FancyZones here; other multi-monitor utilities do the same). A one-shot hide from the application cannot be made robust against that. Not allocating the console, or spawning the child with `CREATE_NO_WINDOW` so the console is headless, removes the window entirely rather than hiding it. Delaying the hide by a few hundred milliseconds would survive FancyZones but is the same fragile idea with a longer fuse.

**For anyone with this symptom.** It needs a multi-monitor setup and something that moves new windows to the cursor's monitor. If you run PowerToys, add `GoogleDriveFS.exe` to FancyZones' excluded apps (Settings, FancyZones, "Excluded apps"); the window then stays hidden and the rest of FancyZones keeps working. The console still exists behind the hidden window, so the click-freeze and close-restart behaviour in the report would return the moment anything shows it.

**For PowerToys.** FancyZones' move re-shows a window that the owning application hid between creation and the move. Preserving the window's visibility state across the move, or skipping windows that are not visible when the move runs, would avoid undoing an application's own hide.
