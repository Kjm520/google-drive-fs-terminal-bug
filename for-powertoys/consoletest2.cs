// Like consoletest, plus Drive's next steps: open CONOUT$ as stderr, make it unbuffered, and write log lines immediately.
// args: [linesToWrite] [holdMs] [logfile]
using System; using System.Diagnostics; using System.IO; using System.Runtime.InteropServices; using System.Text; using System.Threading;
class ConsoleTest2 {
  [DllImport("kernel32.dll")] static extern bool AllocConsole();
  [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateFile(string n, uint a, uint s, IntPtr sa, uint d, uint f, IntPtr t);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteFile(IntPtr h, byte[] b, uint n, out uint w, IntPtr o);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  static string Rect(IntPtr w) { RECT r; GetWindowRect(w, out r); return r.L + "," + r.T + "-" + r.R + "," + r.B; }
  static void Main(string[] args) {
    int lines = args.Length > 0 ? int.Parse(args[0]) : 50;
    int holdMs = args.Length > 1 ? int.Parse(args[1]) : 3000;
    string log = args.Length > 2 ? args[2] : null;
    var sw = Stopwatch.StartNew(); var t0 = DateTime.Now;
    AllocConsole(); long tAlloc = sw.ElapsedMilliseconds;
    IntPtr w = GetConsoleWindow(); string r0 = Rect(w);
    ShowWindow(w, 0); long tHide = sw.ElapsedMilliseconds; bool v1 = IsWindowVisible(w);
    IntPtr h = CreateFile("CONOUT$", 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); long tOpen = sw.ElapsedMilliseconds; bool v2 = IsWindowVisible(w);
    uint n; long tFirstWrite = -1; bool vAfterFirst = false;
    for (int i = 0; i < lines; i++) {
      var b = Encoding.ASCII.GetBytes(string.Format("{0:yyyy-MM-ddTHH:mm:ss.fff}Z line {1} of simulated log output from consoletest2 to the console stderr handle\r\n", DateTime.UtcNow, i));
      WriteFile(h, b, (uint)b.Length, out n, IntPtr.Zero);
      if (i == 0) { tFirstWrite = sw.ElapsedMilliseconds; vAfterFirst = IsWindowVisible(w); }
    }
    long tWrites = sw.ElapsedMilliseconds; bool v3 = IsWindowVisible(w);
    var samples = new StringBuilder(); int[] at = { 5, 20, 100, 500, 1000 };
    foreach (int ms in at) { while (sw.ElapsedMilliseconds < tWrites + ms) Thread.Sleep(0); samples.AppendFormat("+{0}={1} ", ms, IsWindowVisible(w) ? "V" : "h"); }
    string line = string.Format("{0:HH:mm:ss.fff} pid {1} hwnd 0x{2:X} rect {3} | alloc@{4} hide@{5} vis={6} | CONOUT$ open@{7} vis={8} | first write@{9} vis={10} | {11} lines done@{12} vis={13} rect {14} | later: {15}",
      t0, Process.GetCurrentProcess().Id, w.ToInt64(), r0, tAlloc, tHide, v1 ? "V" : "h", tOpen, v2 ? "V" : "h", tFirstWrite, vAfterFirst ? "V" : "h", lines, tWrites, v3 ? "V" : "h", Rect(w), samples);
    if (log != null) File.AppendAllText(log, line + Environment.NewLine);
    Thread.Sleep(Math.Max(0, holdMs - (int)sw.ElapsedMilliseconds));
  }
}
