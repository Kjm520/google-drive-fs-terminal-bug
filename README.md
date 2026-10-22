# Google Drive for desktop (Windows): console window that cannot be closed

Evidence for a long-standing (at least since 2023) defect in Google Drive for desktop on Windows. `GoogleDriveFS.exe` allocates a console window on every child-process start. When the window is visible and the user closes it, the child exits, the parent restarts it with a new window, and after seven restarts Drive stops running until the next sign-in.

## Summary

Every DriveFS child process allocates a console on startup. The DriveFS log records this itself:

```
instrumentation.cc:376:GetStderrFileDescriptor Allocating console to enable stderr.
```

This line appears in 155 of 155 child-process logs retained on this machine (2026-07-21 to 2026-10-21). When that console window is visible, the user sees a black window titled `C:\Program Files\Google\Drive File Stream\<version>\GoogleDriveFS.exe` that fills with log output. Closing it sends the console close event to the child, which exits with `0xC000013A` (STATUS_CONTROL_C_EXIT). The parent process treats that as a crash and starts a new child, which opens a new console window. After 7 restarts the parent stops restarting, and Drive is down until the next sign-in.

So the user-visible defect is a window that "cannot be closed", and the consequence of trying is that Drive dies.


![LS](./googledrivefsexescreenshot20260616blurred.png)


**Read the report:** [REPORT-google-drive-console-window.md](REPORT-google-drive-console-window.md)


## What is in this repository

| File | Contents |
|---|---|
| `REPORT-google-drive-console-window.md` | The full report: mechanism, log evidence, controlled reproduction, prior public reports, in-app feedback history, reproduction steps |
| `child-process-timeline.csv` | Every retained DriveFS child-process start from 2026-07-21 to 2026-10-15: version, start time, lifetime, console allocated, how it ended |
| `in-app-feedback-submissions.csv` | Every in-app "Send feedback" submission about this defect in the same period, with outcome |
| `in-app-bug-reports.txt` | The raw log extract the feedback CSV was built from |
| `existing-reports.md` | Links to earlier public reports of the same symptom in the Google Drive Help Community |
| `googledrivefsexescreenshot20260616blurred.png` | The window on version 126.0.5.0, two weeks after a clean reinstall |
| `console-window-after-parent-restart-2026-10-15.png` | The window after a parent-initiated restart during the controlled reproduction |
| `live-capture-2026-10-21.md` | Live inspection of a window that appeared on its own: the child allocates the console itself, and a click in the window freezes DriveFS logging |

The raw DriveFS log files that back the report are not published here because they contain account and machine identifiers. They have been provided to Google directly.

## Status

Reported through Drive's in-app feedback 26 times between July and October 2026 with diagnostic logs attached, and three times by other users in the Google Drive Help Community (threads 417837966, 434801725, 445097711). No fix as of 2026-10-21 on version 132.0.0.0. On that date the window appeared on its own after a scheduled restart and was captured live (see `live-capture-2026-10-21.md`); closing it afterwards ran the seven-restart loop and left Drive stopped.
