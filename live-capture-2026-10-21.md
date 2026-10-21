# Live capture of the console window, 2026-10-21

On 2026-10-21 at 14:31:34 local (19:31:34Z) the console window appeared on its own, with no user action, on Drive for desktop 132.0.0.0. It was still open two minutes later when this capture began, so for the first time the state could be read from the running processes instead of reconstructed from logs afterwards. Everything below was read between 14:33 and 14:50 local while the window was open. The raw captures (console text, logs, environment, screenshots, thread stacks, a full memory dump) are in the private set provided to Google (`for-google/live-capture-2026-10-21/`).

## 1. What triggered the window: DriveFS's own scheduled restart

The parent process (pid 23536, running since 2026-10-16 16:55 local) logged in `parent.txt`:

```
2026-10-21T19:31:34.906ZE drive_fs_main.cc:748:RunDriveFSChildProcess Process exited with code: 0x11
2026-10-21T19:31:34.906ZI drive_fs_main.cc:896:wWinMain Restarting DriveFS child process due to exit status Restart count: 1
2026-10-21T19:31:34.919ZI drive_fs_main.cc:738:RunDriveFSChildProcess Waiting for DriveFS child process to exit with pid: 30064
```

The previous child (log `drive_fs_200.txt`, alive 4 days 21 hours) had requested that restart itself:

```
2026-10-21T19:31:29.768ZI client.cc:3138:RequestSafeRestart  is requesting RESTART_APP_NOW from global_experiment_fetcher.cc:362:cello::fs::GlobalPhenotypeExperimentFetcher::GetAndPersistExperimentSnapshotsForEnabledAccountsMaybeRestart
2026-10-21T19:31:33.470ZI drive_fs.cc:818:RunDriveFS Return code: APP_RESTART_REQUESTED
```

No crash, no user action. The only Windows events in that minute are the Dokan driver (provider `googledrivefs31931`) unmounting G: at 14:31:29 and mounting it again at 14:31:37. This is the restart path described in section 3 of the report: a child started by the parent's restart path gets a visible console.

## 2. Who creates the console: the child itself, one second after it starts

Read from each process's `RTL_USER_PROCESS_PARAMETERS` (PEB) while both were running:

| Field | Child, pid 30064 | Parent, pid 23536 |
|---|---|---|
| Command line | `GoogleDriveFS.exe --crash_handler_token=\\.\pipe\crashpad_23536_… --parent_version=132.0.0.0 --startup_mode` | `GoogleDriveFS.exe --startup_mode` |
| ConsoleHandle | 0x3A8, object `\Device\ConDrv` | 0 (no console) |
| StandardInput / Output / Error | 0x3AC / 0x3B0 / 0x3B4, all `\Device\ConDrv` | 0 / 0 / 0 |
| WindowFlags (`STARTF_*`) | 0 (nothing requested) | 0x1 `STARTF_USESHOWWINDOW` |
| ShowWindowFlags | 0 | 1 (`SW_SHOWNORMAL`, from the explorer Run-key launch) |
| WindowTitle | the executable path (the `CreateProcess` default) | the executable path |
| DesktopInfo | `Winsta0\Default` | `Winsta0\Default` |

So the parent passed no console, no standard handles, and no show-window request to the child. The child has a console because it allocated one:

- `conhost.exe` pid 33968 was created at 14:31:35, one second after the child (14:31:34), as a child of pid 30064, with the command line `conhost.exe 0x4` (a windowed host, not `conhost.exe --headless`).
- The first line in the console's screen buffer is `2026-10-21T19:31:35.074ZI [29844:log_writer_thread] instrumentation.cc:376:GetStderrFileDescriptor Allocating console to enable stderr.`
- Windows attributes the console window (class `ConsoleWindowClass`, handle 0x206CC) to pid 30064, thread 29844: the same `log_writer_thread`.
- `GoogleDriveFS.exe` is a GUI-subsystem image (PE optional header `Subsystem = 2`). Windows never creates a console for such an image; the only way it gets one is a call to `AllocConsole`.

## 3. The window as found

- Visible, normal state (not minimized, not topmost, not cloaked), 1119 x 648 px, 120 x 30 cells, Consolas 7x16, screen buffer 120 x 9001 lines, code page 437, QuickEdit mode on (input mode 0x1F7). It sat at z-order position 10 among the visible top-level windows, under the editor and browser windows, on the left monitor.
- Title: the executable path, exactly as in the earlier screenshots.
- By 14:43 the buffer held 1,894 non-empty lines of log output. They are the same lines, in the same order, as `drive_fs.txt`. The log is written to the console and to the file by the same thread.

## 4. New finding: a click in the window freezes DriveFS logging

Between 14:34:24 and 14:37:26 local the window entered selection mode: its title gained the `Select` prefix and `GetConsoleSelectionInfo` reported flags 0x7 (selection in progress, not empty, mouse selection). That is what a conhost window with QuickEdit on does when it is clicked. While a selection is active conhost does not complete writes to its screen buffer, so any thread writing to that console blocks.

Observed consequences, which held for 28 minutes until the selection was cleared:

- `drive_fs.txt` stopped at `2026-10-21T19:34:23.772Z` and did not grow again (104,202 bytes). Nothing logged after that line reached the file.
- Thread 29844, the `log_writer_thread`, stayed in a kernel wait the whole time with no CPU time accrued. Its stack, walked live with dbghelp (export symbols only, so the frames inside `GoogleDriveFS.exe` are unnamed):

  ```
  00 ntdll.dll!NtWriteFile+0x14
  01 KERNELBASE.dll!WriteFile+0x8D
  02 GoogleDriveFS.exe+0x33BE436
  03 GoogleDriveFS.exe+0x33BDCE8
  04 GoogleDriveFS.exe+0x33BDECF
  05 GoogleDriveFS.exe+0x33BF63B
  06 GoogleDriveFS.exe+0x33BF56C
  07 GoogleDriveFS.exe+0x33BF879
  08 GoogleDriveFS.exe+0x33BF502
  09 GoogleDriveFS.exe+0x15E4576
  10 GoogleDriveFS.exe+0x15E4B08
  11 GoogleDriveFS.exe+0x13201B5
  12 GoogleDriveFS.exe+0x119F30F
  13 GoogleDriveFS.exe+0x119F82E
  14 GoogleDriveFS.exe+0x33B1703
  15 KERNEL32.DLL!BaseThreadInitThunk+0x17
  16 ntdll.dll!RtlUserThreadStart+0x2C
  ```

  (Offsets are from the image base; the image is `GoogleDriveFS.exe` 132.0.0.0, changelist 983995292. The full stack of every thread is in the private set, and the memory dump contains the same state.)

- The arguments of that pending `NtWriteFile`, read from the thread's saved register context: FileHandle 0x398; Length 173; and the buffer holds the line `2026-10-21T19:37:26.734ZI [30216:mirror_…] cloud_subsystem.cc:192:OnChangeNotificationReceived Generated 1 download events from 1 changelog entries`. That line is not in `drive_fs.txt`: the file write waits behind the console write.
- Handle 0x398 is a write-only file handle (access mask 0x120196, `GENERIC_WRITE`), distinct from the three standard handles, which is what opening `CONOUT$` for stderr after `AllocConsole` produces. An `NtQueryObject` name query on it, started at 14:52, blocked for the rest of the stall, as it does on any synchronous file object with an I/O in progress, while the same query on the console's other handles (0x3A8 to 0x3B4) returned at once. It returned the moment the write completed: `\Device\ConDrv`.
- The mount kept working. A directory listing of `G:\` completed in 36 ms during the stall. The stall is in the logging pipeline, not in the file system.
- **Release.** At 15:02:02.494 local the selection was cleared (the title lost its `Select` prefix). Within 60 ms `drive_fs.txt` grew from 104,202 to 112,566 bytes in one burst: 64 lines, the first of them exactly the line that had been pending in the blocked `WriteFile` (19:37:26.734Z), the last stamped 20:02:02.372Z. The 25 minutes of log lines had been queued in memory behind the console write. They came out in order, but misdated: the other 63 lines carry timestamps between 20:02:02.365Z and 20:02:02.372Z, the release moment, across six threads, among them 26 consecutive curl trace lines for one request, a network response, two sync-engine status dumps and an OAuth credential update. DriveFS stamps a line when the writer thread dequeues it, not when the event happened. So after a stall the log shows a 25-minute silence followed by a 7 ms burst, and the true times of those events are gone. The producing threads were not themselves blocked: a curl request's full trace and its response cannot fit in two milliseconds after being unblocked, and the mount answered normally during the stall.

So besides being unwanted and unclosable, the window is a trap. The natural thing to do with a mystery window that streams text is to click on it to read it, and that click silently stops the product's logging until the selection is cleared (Escape, or a right-click). Any diagnostic bundle collected while the selection is active, including the ones attached to in-app feedback, is truncated at the moment of the click, and one collected after the selection is cleared misdates every event that happened during it.

## 4a. Closing the window afterwards: the restart loop, observed live

After the capture, in-app feedback was submitted and the window was then closed with its X button, repeatedly, until Drive stopped. The parent log (`parent.txt`) and a half-second poll of the child's main window title recorded the sequence. All times UTC.

| Child pid | Log file | Started | Exited | Lifetime | Exit code | Parent log | Notes |
|---|---|---|---|---|---|---|---|
| 30064 | `drive_fs_201.txt` | 19:31:34.9 | 20:06:38.2 | 35 min 3 s | `0xc000013a` | `due to exception Restart count: 1` | the child inspected above |
| 33068 | `drive_fs_202.txt` | 20:06:38.2 | 20:07:59.6 | 81.4 s | `0xc000013a` | `Restart count: 2` | console title seen by the poll; the in-app feedback was submitted from this child: `submit_feedback` 20:07:33.2, `Sending feedback of size: 28510447`, `Feedback submitted successfully` 20:07:53.9 |
| 12280 | `drive_fs_203.txt` | 20:07:59.6 | 20:08:00.6 | 1.0 s | `0xc000013a` | `Restart count: 3` | |
| 22160 | `drive_fs_204.txt` | 20:08:00.6 | 20:08:01.4 | 0.8 s | `0xc000013a` | `Restart count: 4` | |
| 33724 | `drive_fs_205.txt` | 20:08:01.4 | 20:08:02.1 | 0.7 s | `0xc000013a` | `Restart count: 5` | |
| 9048 | `drive_fs_206.txt` | 20:08:02.2 | 20:08:03.1 | 0.9 s | `0xc000013a` | `Restart count: 6` | console title seen by the poll 0.7 s after start |
| 28512 | `drive_fs.txt` | 20:08:03.1 | 20:08:04.5 | 1.4 s | `0xc000013a` | `Restart count: 7` | last child; no restart followed |

Every one of the seven logs contains `instrumentation.cc:376:GetStderrFileDescriptor Allocating console to enable stderr` within 60 to 120 ms of its first line. After restart count 7 the parent wrote nothing more and exited; by 20:09Z there was no `GoogleDriveFS.exe` process at all and `G:` was unmounted. The parent keeps two separate restart counters: the self-requested `0x11` exit at 19:31:34Z was logged as `due to exit status Restart count: 1`, and the first console close at 20:06:38Z as `due to exception Restart count: 1`.

## 5. Contents of the private set

`for-google/live-capture-2026-10-21/` (not published; contains the account ID, hostname, device identifiers and synced file names):

| File | What it is |
|---|---|
| `console-screen-buffer-pid30064.txt` | The full text of the console screen buffer at 14:43 (1,894 lines) |
| `drivefs-logs/parent.txt` | The parent log showing the 0x11 exit and the restart |
| `drivefs-logs/drive_fs_200.txt` | The previous child's full log (2026-10-16 to 2026-10-21), ending in `RequestSafeRestart` |
| `drivefs-logs/drive_fs_201.txt` | The inspected child's complete log (frozen at 19:34:23.772Z during the stall, flushed at 20:02:02Z, last line 20:03:58Z) |
| `drivefs-logs/drive_fs_202.txt` to `drive_fs_206.txt`, `drive_fs.txt` | The six children of the restart loop; `drive_fs_202.txt` contains the feedback submission |
| `drivefs-logs/startup_trace_*.json`, `structured_log_*` | The other log files written by these children |
| `environment-child-pid30064.txt`, `environment-parent-pid23536.txt` | Both processes' environment blocks, read from their PEBs |
| `console-window-screen-capture-14-39-36.png`, `console-window-printwindow-14-39-36.png` | The window as rendered on screen and via `PrintWindow` |
| `full-desktop-14-39-36.png` | All three monitors, showing where the window landed |
| `thread-stacks-pid30064-14-48-47.txt` | Stacks of all 98 threads of the child |
| `release-monitor.log` | Half-second poll of the window title and the log file size across the moment the selection was cleared |
| `raw-process-and-window-state.txt` | Verbatim tool output for everything summarised in this page |
| `GoogleDriveFS-pid30064-full-memory-14-39-44.dmp` | Full memory dump of the child (252 MB, `MiniDumpWithFullMemory` + handle data + thread info). It contains the account's live auth tokens; it should only be transferred through a channel Google treats as confidential. |
| `tool-read-console-buffer.ps1` | The script that attached to the console and read it |

## 6. Method

All reads were made on the live process without stopping it. Process tree and command lines: WMI `Win32_Process`. Startup parameters: `NtQueryInformationProcess` + `ReadProcessMemory` of the PEB's `ProcessParameters`. Handle objects: `DuplicateHandle` + `NtQueryObject`. Window: `GetClassName`, `GetWindowLongPtr`, `GetWindowRect`, `GetWindowThreadProcessId`, `DwmGetWindowAttribute`, z-order via `GetTopWindow`/`GetWindow`. Console: a hidden helper process called `FreeConsole` then `AttachConsole(30064)`, then `GetConsoleSelectionInfo`, `GetConsoleMode`, `GetConsoleScreenBufferInfo`, `ReadConsoleOutputCharacter` row by row. Stacks: `SuspendThread`/`GetThreadContext`/`ResumeThread` and dbghelp `StackWalk64`. Pending syscall arguments: from the saved context at the syscall boundary (`R10` holds the original `RCX` = FileHandle; `[RSP+0x30]` and `[RSP+0x38]` are the Buffer and Length arguments of `NtWriteFile`). Dump: dbghelp `MiniDumpWriteDump`. PE subsystem: read from the optional header of the installed executable.
