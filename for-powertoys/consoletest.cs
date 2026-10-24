// Minimal reproduction of GoogleDriveFS.exe's GetStderrFileDescriptor sequence in a GUI-subsystem process:
// AllocConsole, GetConsoleWindow, ShowWindow(SW_HIDE). Logs its own timing and samples the window's visibility afterwards.
// args: [delayMsBeforeHide] [holdMs] [logfile]
using System; using System.Diagnostics; using System.IO; using System.Runtime.InteropServices; using System.Text; using System.Threading;
class ConsoleTest {
  [DllImport("kernel32.dll")] static extern bool AllocConsole();
  [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  static void Main(string[] args) {
    int delayMs = args.Length > 0 ? int.Parse(args[0]) : 0;
    int holdMs = args.Length > 1 ? int.Parse(args[1]) : 3000;
    string log = args.Length > 2 ? args[2] : null;
    var sw = Stopwatch.StartNew(); var t0 = DateTime.Now;
    bool a = AllocConsole(); long tAlloc = sw.ElapsedMilliseconds;
    IntPtr w = GetConsoleWindow(); bool v0 = IsWindowVisible(w);
    if (delayMs > 0) Thread.Sleep(delayMs);
    long tHideCall = sw.ElapsedMilliseconds; bool prev = ShowWindow(w, 0); long tHideRet = sw.ElapsedMilliseconds; bool v1 = IsWindowVisible(w);
    var samples = new StringBuilder(); int[] at = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000 };
    foreach (int ms in at) { while (sw.ElapsedMilliseconds < tHideRet + ms) Thread.Sleep(0); samples.AppendFormat("+{0}={1} ", ms, IsWindowVisible(w) ? "V" : "h"); }
    string line = string.Format("{0:HH:mm:ss.fff} pid {1} hwnd 0x{2:X} delay={3} AllocConsole ret@{4}ms visAfterAlloc={5} | hide call@{6}ms prevVisible={7} ret@{8}ms visAfterHide={9} | after hide: {10}",
      t0, Process.GetCurrentProcess().Id, w.ToInt64(), delayMs, tAlloc, v0 ? "V" : "h", tHideCall, prev, tHideRet, v1 ? "V" : "h", samples);
    if (log != null) File.AppendAllText(log, line + Environment.NewLine);
    Thread.Sleep(Math.Max(0, holdMs - (int)sw.ElapsedMilliseconds));
  }
}
