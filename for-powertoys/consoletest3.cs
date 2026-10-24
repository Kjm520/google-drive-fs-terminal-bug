// Parent/child harness mimicking GoogleDriveFS.exe's structure. Same exe in both roles.
//   parent mode: consoletest3.exe parent <useJob 0|1> <restarts> <childHoldMs> <logfile>
//     CreateProcess(self, "child ..."), with a zeroed STARTUPINFO and no creation flags (as Drive's parent does),
//     optionally AssignProcessToJobObject, wait for exit, log, restart. Exits after <restarts> restarts.
//   child mode:  consoletest3.exe child <holdMs> <logfile>
//     AllocConsole, GetConsoleWindow, ShowWindow(SW_HIDE), open CONOUT$, write 200 lines, log, hold, exit.
using System; using System.Diagnostics; using System.IO; using System.Runtime.InteropServices; using System.Text; using System.Threading;
class ConsoleTest3 {
  [DllImport("kernel32.dll")] static extern bool AllocConsole();
  [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateFile(string n, uint a, uint s, IntPtr sa, uint d, uint f, IntPtr t);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteFile(IntPtr h, byte[] b, uint n, out uint w, IntPtr o);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CreateProcessW(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
  [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateJobObjectW(IntPtr a, string name);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetInformationJobObject(IntPtr j, int cls, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, int len);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr j, IntPtr p);
  [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr h, uint ms);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool CloseHandle(IntPtr h);
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct STARTUPINFO { public int cb; public string lpReserved, lpDesktop, lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
  [StructLayout(LayoutKind.Sequential)] struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
  [StructLayout(LayoutKind.Sequential)] struct JOBOBJECT_BASIC_LIMIT_INFORMATION { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
  [StructLayout(LayoutKind.Sequential)] struct IO_COUNTERS { public ulong a, b, c, d, e, f; }
  [StructLayout(LayoutKind.Sequential)] struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION { public JOBOBJECT_BASIC_LIMIT_INFORMATION Basic; public IO_COUNTERS Io; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
  static void Log(string path, string s) { File.AppendAllText(path, string.Format("{0:HH:mm:ss.fff} {1}", DateTime.Now, s) + Environment.NewLine); }
  static int Main(string[] args) {
    if (args[0] == "child") {
      int holdMs = int.Parse(args[1]); string log = args[2]; int hideDelay = args.Length > 3 ? int.Parse(args[3]) : 0;
      var sw = Stopwatch.StartNew();
      AllocConsole(); long tAlloc = sw.ElapsedMilliseconds; IntPtr w = GetConsoleWindow();
      if (hideDelay > 0) Thread.Sleep(hideDelay);
      ShowWindow(w, 0); long tHide = sw.ElapsedMilliseconds; bool v1 = IsWindowVisible(w);
      IntPtr h = CreateFile("CONOUT$", 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); uint n;
      for (int i = 0; i < 200; i++) { var b = Encoding.ASCII.GetBytes(string.Format("{0:o} line {1} from child\r\n", DateTime.UtcNow, i)); WriteFile(h, b, (uint)b.Length, out n, IntPtr.Zero); }
      long tW = sw.ElapsedMilliseconds; Thread.Sleep(50); bool v2 = IsWindowVisible(w); Thread.Sleep(500); bool v3 = IsWindowVisible(w);
      Log(log, string.Format("CHILD pid {0} hwnd 0x{1:X} alloc@{2} hide@{3} vis={4} writes done@{5} vis+50ms={6} vis+550ms={7}", Process.GetCurrentProcess().Id, w.ToInt64(), tAlloc, tHide, v1 ? "V" : "h", tW, v2 ? "V" : "h", v3 ? "V" : "h"));
      Thread.Sleep(holdMs); return 0;
    }
    bool useJob = args[1] == "1"; int restarts = int.Parse(args[2]); int childHold = int.Parse(args[3]); string plog = args[4]; string hideDelayArg = args.Length > 5 ? args[5] : "0";
    string exe = Process.GetCurrentProcess().MainModule.FileName;
    IntPtr job = IntPtr.Zero;
    if (useJob) { job = CreateJobObjectW(IntPtr.Zero, null); var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION(); info.Basic.LimitFlags = 0x2000; SetInformationJobObject(job, 9, ref info, Marshal.SizeOf(info)); }
    Log(plog, string.Format("PARENT pid {0} useJob={1}", Process.GetCurrentProcess().Id, useJob));
    for (int i = 0; i <= restarts; i++) {
      var si = new STARTUPINFO(); si.cb = Marshal.SizeOf(si); PROCESS_INFORMATION pi;
      string cmd = string.Format("\"{0}\" child {1} \"{2}\" {3}", exe, childHold, plog, hideDelayArg);
      if (!CreateProcessW(null, cmd, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, null, ref si, out pi)) { Log(plog, "CreateProcess failed " + Marshal.GetLastWin32Error()); return 1; }
      if (useJob && !AssignProcessToJobObject(job, pi.hProcess)) Log(plog, "AssignProcessToJobObject failed " + Marshal.GetLastWin32Error());
      Log(plog, string.Format("PARENT started child pid {0} (start #{1})", pi.dwProcessId, i));
      WaitForSingleObject(pi.hProcess, 0xFFFFFFFF); uint code; GetExitCodeProcess(pi.hProcess, out code);
      Log(plog, string.Format("PARENT child {0} exited 0x{1:X}; restarting", pi.dwProcessId, code));
      CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
    }
    return 0;
  }
}
