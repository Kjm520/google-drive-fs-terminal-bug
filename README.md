# Google Drive for desktop (Windows): console window that cannot be closed

Evidence for a long-standing defect in Google Drive for desktop on Windows. `GoogleDriveFS.exe` allocates a console window on every child-process start. When the window is visible and the user closes it, the child exits, the parent restarts it with a new window, and after seven restarts Drive stops running until the next sign-in.

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

The raw DriveFS log files that back the report are not published here because they contain account and machine identifiers. They have been provided to Google directly.

## Status

Reported through Drive's in-app feedback 25 times between July and October 2026 with diagnostic logs attached, and three times by other users in the Google Drive Help Community (threads 417837966, 434801725, 445097711). No fix as of 2026-10-15 on version 132.0.0.0.
