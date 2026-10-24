# Records every create/show/hide/destroy of a ConsoleWindowClass window, with the owning process,
# to a log file. Used to time Drive's hide call against conhost's show. Read-only; touches nothing.
# Usage: pwsh -NoProfile -File console-window-events.ps1 <logfile>
param([string]$LogPath)
Add-Type @"
using System; using System.Collections.Generic; using System.Diagnostics; using System.IO; using System.Runtime.InteropServices; using System.Text;
public static class ConsoleWindowEvents {
  public delegate void WinEventDelegate(IntPtr hHook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
  [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventDelegate cb, uint pid, uint tid, uint flags);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint a, uint b);
  [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
  [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
  [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenThread(uint a, bool i, uint tid);
  [DllImport("kernel32.dll")] static extern uint GetProcessIdOfThread(IntPtr h);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  static string ByThread(uint tid) {
    IntPtr h = OpenThread(0x800, false, tid); if (h == IntPtr.Zero) return "tid " + tid + " (?)";
    uint pid = GetProcessIdOfThread(h); CloseHandle(h); string n = "?";
    try { n = Process.GetProcessById((int)pid).ProcessName; } catch {}
    return string.Format("by {0} pid {1} tid {2}", n, pid, tid);
  }
  [StructLayout(LayoutKind.Sequential)] public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  static StreamWriter log; static WinEventDelegate keep;
  static Dictionary<long, string> known = new Dictionary<long, string>();
  static HashSet<uint> seenPids = new HashSet<uint>();
  static void Cb(IntPtr hHook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time) {
    try {
      if (idObject != 0 || idChild != 0 || hwnd == IntPtr.Zero) return;
      long key = hwnd.ToInt64(); string tag;
      if (evt == 0x8001) { if (!known.TryGetValue(key, out tag)) return; known.Remove(key); }
      else {
        var sb = new StringBuilder(64); GetClassName(hwnd, sb, 64); if (sb.ToString() != "ConsoleWindowClass") return;
        uint pid; GetWindowThreadProcessId(hwnd, out pid);
        string pname = "?"; DateTime? start = null;
        try { var p = Process.GetProcessById((int)pid); pname = p.ProcessName; start = p.StartTime; } catch {}
        if (pname == "pwsh" || pname == "powershell" || pname == "cmd" || pname == "OpenConsole" || pname == "WindowsTerminal") return;
        if (!seenPids.Contains(pid) && start.HasValue) { seenPids.Add(pid); log.WriteLine("{0:HH:mm:ss.fff}  START    {1} pid {2}", start.Value, pname, pid); }
        tag = string.Format("{0} pid {1}  hwnd 0x{2:X}", pname, pid, key);
        if (start.HasValue) tag += string.Format("  t+{0}ms", (long)(DateTime.Now - start.Value).TotalMilliseconds);
        known[key] = tag;
      }
      string name = evt == 0x8000 ? "CREATE" : evt == 0x8002 ? "SHOW" : evt == 0x8003 ? "HIDE" : evt == 0x8001 ? "DESTROY"
        : evt == 0x0003 ? "FOREGRND" : evt == 0x8004 ? "REORDER" : evt == 0x8005 ? "FOCUS" : evt == 0x800B ? "LOCATION" : evt == 0x800C ? "NAME"
        : evt == 0x0016 ? "MINSTART" : evt == 0x0017 ? "MINEND" : ("0x" + evt.ToString("X"));
      RECT rc; GetWindowRect(hwnd, out rc);
      log.WriteLine("{0:HH:mm:ss.fff}  {1,-8} {2}  visible={3}  rect {4},{5}-{6},{7}", DateTime.Now, name, tag, IsWindowVisible(hwnd), rc.L, rc.T, rc.R, rc.B);
      log.Flush();
    } catch (Exception e) { log.WriteLine("callback error: " + e.Message); log.Flush(); }
  }
  public static void Run(string path) {
    log = new StreamWriter(path, true);
    log.WriteLine("{0:HH:mm:ss.fff}  HOOK     started, recorder pid {1}", DateTime.Now, Process.GetCurrentProcess().Id); log.Flush();
    keep = Cb;
    IntPtr h = SetWinEventHook(0x0001, 0x800C, IntPtr.Zero, keep, 0, 0, 0);
    if (h == IntPtr.Zero) { log.WriteLine("SetWinEventHook failed"); log.Flush(); return; }
    MSG m; while (GetMessage(out m, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref m); DispatchMessage(ref m); }
  }
}
"@
[ConsoleWindowEvents]::Run($LogPath)
