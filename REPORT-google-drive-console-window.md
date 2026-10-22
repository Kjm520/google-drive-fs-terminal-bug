# Drive for desktop (Windows): GoogleDriveFS.exe opens a console window on every child-process start; closing it causes a restart loop and then Drive stops

**Product:** Google Drive for desktop, Windows
**Versions observed:** 126.0.5.0 (screenshot, 2026-06-16), 128.0.0.0, 129.0.1.0, 130.0.2.0, 131.0.2.0, 132.0.0.0 (current). The behaviour is identical across all six; it has been present for years on this machine.
**OS:** Windows 11 Pro 10.0.26300, x64, 24 logical processors, 32 GB RAM
**Account type:** Workspace account, mirror mode, mounted as G:
**Previous reports:** 26 in-app feedback reports delivered on 14 separate days between 2026-07-21 and 2026-10-21, through Drive's own "Send feedback" dialog, on versions 128 through 132, every one with diagnostic logs attached (see section 6). No response to any of them.


## What is in this repository

| File | Contents |
|---|---|
| `REPORT-google-drive-console-window.md` | This full report: mechanism, log evidence, controlled reproduction, prior public reports, in-app feedback history, reproduction steps |
| `child-process-timeline.csv` | Every retained DriveFS child-process start from 2026-07-21 to 2026-10-15: version, start time, lifetime, console allocated, how it ended |
| `in-app-feedback-submissions.csv` | Every in-app "Send feedback" submission about this defect in the same period, with outcome |
| `in-app-bug-reports.txt` | The raw log extract the feedback CSV was built from |
| `existing-reports.md` | Links to earlier public reports of the same symptom in the Google Drive Help Community |
| `googledrivefsexescreenshot20260616blurred.png` | The window on version 126.0.5.0, two weeks after a clean reinstall |
| `console-window-after-parent-restart-2026-10-15.png` | The window after a parent-initiated restart during the controlled reproduction |
| `live-capture-2026-10-21.md` | Live inspection of a window that appeared on its own: the child allocates the console itself, and a click in the window freezes DriveFS logging |

The raw DriveFS log files that back the report are not published here because they contain account and machine identifiers. They have been provided to Google directly.


## Evidence

### 1. The parent log for 2026-10-13 (`parent_30.txt`)

```
2026-10-14T01:24:26.253ZE drive_fs_main.cc:748:RunDriveFSChildProcess Process exited with code: 0xc000013a
2026-10-14T01:24:26.254ZI drive_fs_main.cc:896:wWinMain Restarting DriveFS child process due to exception Restart count: 1
2026-10-14T01:24:26.657ZI drive_fs_main.cc:738:RunDriveFSChildProcess Waiting for DriveFS child process to exit with pid: 29580
2026-10-14T01:24:28.184ZE drive_fs_main.cc:748:RunDriveFSChildProcess Process exited with code: 0xc000013a
... (repeats) ...
2026-10-14T01:24:32.607ZI drive_fs_main.cc:896:wWinMain Restarting DriveFS child process due to exception Restart count: 7
```

Seven children exited with `0xC000013A` within 6 seconds, one per window closed. No further child was started after restart count 7. The matching child logs are `drive_fs_190.txt` to `drive_fs_195.txt`; each one is a normal startup cut off within about one second.

### 2. Frequency

From the 155 retained child logs (`child-process-timeline.csv`):

| Measure | Count |
|---|---|
| Child starts that allocated a console | 155 of 155 |
| Children that ended under 5 s after start (window closed) | 71 |
| Bursts of 5 or more such closes within a minute | 12 (one every 1 to 2 weeks) |
| Self-requested restarts (`RequestSafeRestart`, exit `0x11`) | 40 |

Burst dates (local): 07-21, 07-27, 07-29, 08-10, 08-11, 08-14, 09-03, 09-10, 09-21, 10-04, 10-13, 10-21.

The 39 self-requested restarts matter because each one also spawns a fresh child, and therefore a fresh console. The restart reason logged is:

```
client.cc:3138:RequestSafeRestart  is requesting RESTART_APP_NOW from global_experiment_fetcher.cc:362:cello::fs::GlobalPhenotypeExperimentFetcher::GetAndPersistExperimentSnapshotsForEnabledA...
drive_fs.cc:818:RunDriveFS Return code: APP_RESTART_REQUESTED
```

### 3. Controlled reproduction, 2026-10-15 (`parent_2026-10-15_experiment.txt`)

All steps below were run on version 132.0.0.0 with the default autostart entry
`"C:\Program Files\Google\Drive File Stream\132.0.0.0\GoogleDriveFS.exe" --startup_mode`.

1. State at sign-in: parent pid 24344 (started by explorer.exe), child pid 14188. Enumerating the child's top-level windows showed a `ConsoleWindowClass` window that existed but was hidden. Console host: `conhost.exe`. Windows Terminal was not involved (no `WindowsTerminal.exe` or `OpenConsole.exe` attached; default terminal setting is "Let Windows decide").
2. Terminated child 14188. Parent logged `Process exited with code: 0xffffffff` and `Restarting DriveFS child process due to exception Restart count: 1`, then started child 17544.
3. Child 17544's `ConsoleWindowClass` window was **visible** (screenshot attached: `console-window-after-parent-restart-2026-10-15.png`). Its command line was identical to child 14188's. Host was again `conhost.exe`.
4. Stopped both processes. Launched the parent from PowerShell with `-WindowStyle Hidden` and stderr redirected to a file: the parent wrote its own log lines to the redirected stderr (`stderr_capture.txt`), but the child still logged `Allocating console to enable stderr` and had a hidden console window.
5. Stopped both processes. Launched the parent with the default window style (same as the Run key): the first child's console window was **visible**.
6. Hiding that window with `ShowWindow(hwnd, SW_HIDE)` left the child running normally and G: mounted. Only *closing* the window kills the child.

Observed rule: the first child's console visibility follows how the parent was launched; a child started by the parent's restart path gets a visible console even when the first child's was hidden.

### 3a. DriveFS already tries to hide the console, and the hide is unreliable

A public thread (support.google.com/drive/thread/434801725, see section 5) quotes these lines from a DriveFS log on another machine:

```
instrumentation.cc:365:...::GetStderrFileDescriptor 5: Access is denied.
instrumentation.cc:366:GetStderrFileDescriptor Can't hide the console.
```

So the code path is AllocConsole, then an attempt to hide the console window. On this machine the "Can't hide the console" line never appears in any of the 147 child logs: the hide call reports success, yet the window is visible in the cases described above. That is consistent with a race between DriveFS hiding the window and the console host showing it during its own initialisation. A hide that depends on timing cannot be made reliable from the application side; not allocating the console is the fix.

### 4. Earlier wrong diagnosis, for completeness

In June 2026 this was attributed to leftover install artifacts (`launch.bat`, `deleteonreboot`, Google Update `InstallerError=9`, "partially deleted previous installation"). Drive was fully uninstalled, the install directory and `%LOCALAPPDATA%\Google\DriveFS` were deleted, and Drive was reinstalled clean on 2026-06-02. The window was back within two weeks on the freshly installed 126.0.5.0 (`googledrivefsexescreenshot20260616blurred.png`, taken 2026-06-16, showing the console titled with the 126.0.5.0 executable path and filled with mirror and EMM log lines; machine identifiers blurred). The behaviour then continued unchanged through five subsequent updates, and the 132.0.0.0 update on 2026-10-07 recreated every one of those artifacts. They are not related.

### 5. Prior public reports of the same defect (Google Drive Help Community)

- **thread/445097711, "GoogleDriveFS.exe misbehaving", 2026-06-25, Windows 11.** "A window opens for GoogleDriveFS.exe and won't close. If I attempt to close it manually, the window re-opens on its own." Suggested fixes were reinstalling WebView2 and a system restore; neither helped. The poster's own workaround on 2026-06-28: disable Drive in Startup Apps and launch it from a taskbar shortcut instead, after which "the window does not open at all". That matches section 3: visibility depends on how the parent is launched. It is not a workaround, though. It only affects the first child. DriveFS restarts its child on its own (`RequestSafeRestart ... RESTART_APP_NOW`, 39 times on this machine since July, roughly every few hours of uptime), and in the controlled reproduction a child started by the parent's restart path got a visible console even when the first child's console was hidden. So the window comes back while the app is running, however it was launched. The thread was closed with "glad to hear you could find a workaround"; the defect was not addressed.
- **thread/434801725, "Mantiene una pantalla negra que no puedo cerrar", 2026-05-18, corporate laptop.** "Al arrancar Windows Google Drive mantiene esta pantalla negra abierta que si cierro vuelve a aparecer." Over 12 replies the poster was walked through cache deletion, full reinstall, clean boot, folder permissions and a Handle.exe scan; none helped. On 2026-05-22 the poster pasted the "Can't hide the console" log lines quoted in section 3a. The thread ended 2026-06-04 without a fix.
- **thread/417837966, "Every time I open Google Drive File Stream it opens up a Terminal".** Same symptom, older report; body not retrievable from the page.

### 6. In-app feedback already submitted for this defect

DriveFS logs each feedback submission (`feedback_dialog.cc OnQuery submit_feedback`, `feedback_recorder.cc SubmitAnonymously`, and the result). The retained logs begin 2026-07-21; reports before that date are not recoverable locally.

The table below is grouped by day, and each day was one occurrence: the window appeared, and closing it ran the restart loop. Where more than one report was sent on a day, all of them were sent during that one occurrence, either one from each successive console window or the same report retried after a delivery failure. The per-submission list, with timestamps, payload sizes and results, is in `in-app-feedback-submissions.csv`.

| Date (local) | Drive version | Reports sent | Delivered | Notes |
|---|---|---|---|---|
| 2026-07-21 | 128.0.0.0 | 11 | 11 | One restart loop. One report from each of 11 successive console windows, each with that window's output pasted in. "GoogleDriveFS.exe won't stop opening. Logs: …" |
| 2026-07-27 | 128.0.0.0 | 8 | 2 | One loop. The same report retried 7 times across two successive windows at 16:58–17:01 (5 failed `DEADLINE_EXCEEDED`, 1 no result logged, 1 delivered), then one more from the next window at 17:05. "Hey a terminal from GoogleDriveFS.exe keeps appearing and printing a lot of logs. If I try to close …" |
| 2026-07-29 | 128.0.0.0 | 1 | 1 | "GoogleDriveFS.exe unwanted terminal popups. This happens about 8 or 9 times in a row. If I try to …" |
| 2026-08-05 | 128.0.0.0 | 2 | 2 | One loop, a report from each of two windows 44 seconds apart. "CANNOT CLOSE GOOGLEDRIVEFS.EXE TERMINAL WINDOW" |
| 2026-08-10 | 129.0.1.0 | 1 | 1 | "I'm getting an unkillable popup terminal from GoogleDriveFS.exe. If I close it, it opens again …" |
| 2026-08-14 | 129.0.1.0 | 1 | 1 | "GoogleDriveFS.exe unwanted and unkillable terminals appearing repeatedly. Something about …" |
| 2026-09-02 | 130.0.2.0 | 1 | 1 | "I'm getting constant unwanted and unkillable GoogleDriveFS.exe terminal popups. It opens and prints …" |
| 2026-09-09 | 130.0.2.0 | 1 | 1 | "Unwanted GoogleDriveFS.exe terminal pop ups. I try to close it and it reopens." |
| 2026-09-10 | 130.0.2.0 | 1 | 0 | No result logged. "I'm still getting unwanted GoogleDriveFS.exe terminal popups that reappear if you try to close them. …" |
| 2026-09-21 | 131.0.2.0 | 1 | 1 | "still getting GoogleDriveFS.exe unwanted popups. If I close it it reopens like 9 or 10 times in a row. …" |
| 2026-09-24 | 131.0.2.0 | 1 | 1 | "GoogleDriveFS.exe continues to open and reopen even after I close it. It happens several times and …" |
| 2026-10-04 | 131.0.2.0 | 5 | 2 | One report from one window, retried after `DEADLINE_EXCEEDED` (3 failed, 2 delivered). "GoogleDriveFS.exe won't stop appearing. If I close it it reopens a new terminal and prints pages …" |
| 2026-10-08 | 132.0.0.0 | 1 | 1 | "GoogleDriveFS.exe continues to pop up and print tons of logs and then stop. Once I close the terminal …" |
| 2026-10-21 | 132.0.0.0 | 1 | 1 | Sent from the child restarted after the live capture (section 7), with a link to this repository. "GoogleDriveFS.exe 132.0.0.0 opened its console window again today on its own after its own RequestSafeRestart …" |
| **Total: 14 days** | | **36** | **26** | 8 failed `DEADLINE_EXCEEDED`, 2 with no result logged |

Every delivered report carried the diagnostic log bundle (13 to 28 MB each), so Google already holds the logs for this machine across five versions. 8 of the 36 submissions failed with `DEADLINE_EXCEEDED`, so the feedback path itself is unreliable at these payload sizes.

### 7. Live capture, 2026-10-21

On 2026-10-21 at 14:31 local the window appeared on its own, after a self-requested restart (`RequestSafeRestart ... RESTART_APP_NOW`, child exit `0x11`, parent `Restart count: 1`), and was inspected while it was open. Full detail and method in [live-capture-2026-10-21.md](live-capture-2026-10-21.md). Findings:

- **The child allocates the console itself.** Read from the running processes' PEBs: the parent passes no console, no standard handles and no `STARTF_USESHOWWINDOW` to the child. The child's console handle and its three standard handles are `\Device\ConDrv` handles created by its own `AllocConsole`; `conhost.exe 0x4` (a windowed host) is spawned by the child one second after it starts; and Windows attributes the console window to the child's `log_writer_thread`, the thread that logs `Allocating console to enable stderr`. `GoogleDriveFS.exe` is a GUI-subsystem image, so this is the only way it can have a console at all.
- **Clicking in the window freezes DriveFS logging.** The conhost window has QuickEdit on, so a click starts a selection, and while a selection is active conhost blocks writers. The `log_writer_thread` was found blocked in `WriteFile` on the console handle; `drive_fs.txt` stopped at the same instant (19:34:23Z) and stayed frozen for 28 minutes, until the selection was cleared. At that moment 64 queued lines were written in one burst: the first was the line found pending in the blocked call's buffer (19:37:26Z), the other 63 are stamped with the release time (20:02:02Z) rather than the time of the events they describe, because DriveFS stamps a line when the writer dequeues it. The G: mount kept working throughout. Any diagnostic bundle collected while such a selection is active is truncated at the click, and one collected afterwards misdates everything that happened during it.
- **The restart loop, observed live.** After the feedback above was submitted from the restarted child (pid 33068, whose console window was also visible), the window was closed with its X button and the loop from section 1 ran under observation: each child exited `0xc000013a`, the parent logged `Restarting DriveFS child process due to exception Restart count: N` and started a new child, and every one of the seven children logged `Allocating console to enable stderr`. After restart count 7 (20:08:04Z) no child was started, the parent process exited too, G: unmounted, and Drive was gone until relaunched by hand. The two counters are independent: the self-requested `0x11` restart earlier that day was logged as `due to exit status Restart count: 1`, and the first console close that followed was again `due to exception Restart count: 1`.

## Steps to reproduce

1. Windows 11, Drive for desktop 132.0.0.0 signed in and running normally.
2. In Task Manager (or `Stop-Process`), end the DriveFS child process (the `GoogleDriveFS.exe` instance whose command line contains `--crash_handler_token`), leaving the parent (`--startup_mode`) running.
3. The parent restarts the child. A console window titled with the GoogleDriveFS.exe path appears and fills with log output.
4. Close that window with its X button.
5. Expected: the window should never exist; at minimum, closing it should not affect Drive.
   Actual: the child exits `0xC000013A`, the parent restarts it, a new window appears. Repeat 7 times and the parent gives up and exits; the G: drive unmounts and Drive stays down until it is launched again.

## Suggested fix

- A GUI-subsystem process should not call `AllocConsole` to obtain a stderr handle. Log to the file sink only, or open `NUL` for stderr when no handle is inherited.
- Independently, the child restart path should pass the same hidden window state as the initial launch, and `STATUS_CONTROL_C_EXIT` from a console close should not be treated as a crash to retry 7 times.
- For as long as the console exists, the console write must not sit in front of the file write on the logging thread. A blocked console (QuickEdit selection, a paused or scrolled window) currently stops the file log too (section 7).

## Attached files

The raw log files are provided to Google only (they contain the account ID, hostname, device IDs and synced file names). The report, the two CSV files and the two screenshots carry no such identifiers and are the public set.

| File | What it is |
|---|---|
| `parent_30.txt` | Parent log 2026-10-09 to 2026-10-13: scheduled `0x11` restarts and the seven `0xC000013A` restarts |
| `drive_fs_189.txt` | The child that was running when the 10-13 burst began |
| `drive_fs_190.txt` to `drive_fs_195.txt` | The six burst children, each cut off within ~1 s |
| `drive_fs_173.txt` | 2026-10-04 burst; contains the in-app feedback submission at 18:05:17 |
| `drive_fs_85.txt`, `drive_fs_86.txt` | 2026-07-29 burst on 128.0.0.0: a self-requested restart followed immediately by window closes |
| `parent_2026-10-15_experiment.txt` | Parent log for the controlled reproduction |
| `stderr_capture.txt` | Parent's stderr when launched with stderr redirected to a file |
| `googledrivefsexescreenshot20260616blurred.png` | Screenshot of the console window on 126.0.5.0, 2026-06-16, two weeks after the clean reinstall (machine identifiers blurred) |
| `console-window-after-parent-restart-2026-10-15.png` | Screenshot of the console window after a parent-initiated restart |
| `child-process-timeline.csv` | All retained child starts: version, start time, lifetime, console allocated, how it ended |
| `in-app-feedback-submissions.csv` | All 36 in-app feedback attempts since 2026-07-21, one row per submission: timestamp, version, payload size, delivered or failed |
| `live-capture-2026-10-21.md` | Live inspection of a window that appeared on its own on 2026-10-21 (public) |
| `live-capture-2026-10-21/` | Private set for that capture: console text, logs, environment, screenshots, all thread stacks, full memory dump of the child |
