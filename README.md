# Google Drive for desktop: console window that keeps respawning

## Summary

Information for a long-standing defect in Google Drive for desktop on Windows. `GoogleDriveFS.exe` allocates a console window on every *child-process* start, and normally hides it but can fail. When that fails, the console persists, and the user closes it, the child exits, parent thinks it crashed, and thus the parent respawns it. Drive doesn't attempt to hide the console on respawns. Meaning we get an annoying 2002-era Internet Explorer-style popup that immediately reappears after closing. The console respawns 7 times before Drive finally quits entirely (parent and all).

![LS](./supporting/googledrivefsexescreenshot20260616blurred.png)

Google Drive fetches its config flags every 4 hours. When a changed flag needs a restart to apply, the child restarts itself `client.cc:3138:RequestSafeRestart`, and every restarted child opens a new console. That's what made the window appear on its own, 2 or 3 times a week IME.

## The Cause

Google Drive allocates a console it should never have allocated, then tries to hide it; everything else follows from that. FancyZones' relocation exposes that. 

Microsoft PowerToy's FancyZones configuration option for "Move newly created windows to the current active monitor" (FancyzonesOpenWindowOnActiveMonitor) is what breaks Drive's normal hide procedure. Drive spawns the child and immediately hides it, but a window relocation prevents that. Which means this only applies to users that have multiple monitors, and have FancyZones with FancyzonesOpenWindowOnActiveMonitor enabled.

**Read Claude's report:** [REPORT-google-drive-console-window.md](REPORT-google-drive-console-window.md)

## Workaround

Disable FancyzonesOpenWindowOnActiveMonitor or add GoogleDriveFS.exe to the list of excluded apps.

## Status

- 2026-10-24; 132.0.0.0: Workaround: GoogleDriveFS.exe added to FancyZones excluded apps; window no longer appears. Console still exists, hidden
- 2026-10-24; Trigger found: FancyZones "Move newly created windows to the current active monitor" re-shows the console after Drive hides it; single monitor cannot reproduce
- 2026-10-24; Confirmed Drive's own hide succeeds every time (event recorder, minimal test programs)
- 2026-10-23; 132.0.0.0: Reproducible on demand: kill the child, the parent's restart opens the window
- 2026-10-22; Help Community reply: reinstall Drive, Chrome, Edge, WebView2 (already done in June; not related)
- 2026-10-22; Help Community: create [new post reporting the problem](https://support.google.com/drive/thread/474105589?hl=en)
- 2026-10-21; 132.0.0.0: Window appeared on its own after a flag-driven self-restart; captured live; 26th in-app feedback sent with repo link
- 2026-10-15; 132.0.0.0: Controlled reproduction; SW_HIDE leaves Drive running, closing kills the child
- 2026-10-13; 132.0.0.0: Seven-close loop; Drive down until relaunched
- 2026-10-08; 132.0.0.0: Update; unchanged
- 2026-09-21; 131.0.2.0: Update; unchanged
- 2026-08-27; 130.0.2.0: Update; unchanged
- 2026-08-05; 129.0.1.0: Update; unchanged
- 2026-07-21; 128.0.0.0: Oldest retained logs; 11 feedback reports sent from one loop
- 2026-06-16; 126.0.5.0: Screenshot, two weeks after the clean reinstall
- 2026-06-02; 126.0.5.0: Clean reinstall; bug back within two weeks

## What has been tried on this machine

- **Clean reinstall, 2026-06-02.** Drive uninstalled, the install directory and `%LOCALAPPDATA%\Google\DriveFS` deleted, Drive reinstalled. The window was back within two weeks on the fresh 126.0.5.0 install.
- **Update software.** 128.0.0.0 through 132.0.0.0. The behaviour is identical on all of them.
- **Launching the parent with a hidden window and stderr redirected to a file.** The child still allocates its own console. The first child's window stays hidden, but the next self-requested restart (DriveFS does this every few hours) starts a child with a visible one.
- **Hiding the window with `ShowWindow(SW_HIDE)`.** Drive keeps running, but the window returns with the next child restart.
- **Terminal settings.** Checked and not involved: the host is `conhost.exe`, Windows Terminal is not attached, and the setting is "Let Windows decide".
- **Shortcuts and startup entries.** Every shortcut and executable reference to `GoogleDriveFS.exe`, including the autostart (Run key) entry, was checked. None carries a debug, verbose or logging flag, or any argument at all beyond Drive's own `--startup_mode` on the autostart entry. The child's command line is built by the parent process.
- **Waiting for the console to finish.** The process never "finishes" that I know of. Google Drive continues to operate in the background and the console window stays open. After closing the window 7-8 times, the app shuts down. Until then, it keeps trying to reopen the console.
