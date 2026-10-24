# Reproduction for PowerToys: FancyZones re-shows a window the app hid

FancyZones' option "Move newly created windows to the current active monitor" moves a new top-level window to the monitor the cursor is on, 10 to 20 ms after it first appears, and the move shows the window again. If the owning application hid the window in between, the hide is undone. Google Drive for desktop does exactly that with the console it allocates for its logger, which is how this was found; the programs here reproduce it without Drive. Measurements are in [../supporting/hide-defeated-by-window-relocation-2026-10-24.md](../supporting/hide-defeated-by-window-relocation-2026-10-24.md), the full event log in `../supporting/console-window-events-2026-10-24.log`.

## Files

| File | What it is |
|---|---|
| `consoletest.cs` | Minimal GUI-subsystem program: `AllocConsole`, `GetConsoleWindow`, `ShowWindow(SW_HIDE)`, then samples the window's visibility for a second. Args: `[delayMsBeforeHide] [holdMs] [logfile]` |
| `consoletest2.cs` | Same, plus opening `CONOUT$` and writing log lines immediately after the hide, as Drive does. Args: `[linesToWrite] [holdMs] [logfile]` |
| `consoletest3.cs` | Parent/child harness with Drive's process structure: the parent spawns the child with a zeroed `STARTUPINFO`, optionally assigns a job object, waits, restarts. `parent <useJob 0|1> <restarts> <childHoldMs> <logfile> [hideDelayMs]` |
| `console-window-events.ps1` | `SetWinEventHook` recorder: logs create/show/hide/move/title/destroy of every `ConsoleWindowClass` window with owning process, timestamp, rectangle and time since process start. `pwsh -File console-window-events.ps1 <logfile>` |

## Build

Any Windows machine has the .NET Framework compiler:

```
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /optimize /out:consoletest.exe consoletest.cs
```

`/target:winexe` matters: the process must be GUI-subsystem so that it has no console until it allocates one, like Drive.

## Reproduce

1. Two or more monitors. FancyZones on, "Move newly created windows to the current active monitor" on.
2. Start the recorder: `pwsh -File console-window-events.ps1 events.log`.
3. Put the cursor on a non-primary monitor.
4. Run `consoletest.exe 0 3000 test.log`.

Expected: `SHOW`, `HIDE`, nothing else; the window stays hidden. Observed with the option on: `SHOW`, `HIDE`, `LOCATION` (moved to the cursor's monitor), `SHOW`; the window is visible. With the option off, with the exe in FancyZones' excluded apps, with the cursor on the primary monitor, or with `consoletest.exe 400 3000 test.log` (hide after the move), it stays hidden.

`consoletest3.exe parent 0 3 20000 harness.log` runs the restart loop; kill the child process repeatedly and each restart reproduces it.
