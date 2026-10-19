using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace JoystickKeeper;

public static class GameLauncher
{
    public static bool IsGameRunning() => Process.GetProcessesByName("StarCitizen").Length > 0;
    public static bool IsRsiLauncherRunning() => Process.GetProcessesByName("RSI Launcher").Length > 0;

    /// <summary>Finds "RSI Launcher.exe": saved path first, then the uninstall registry entry, then the usual folders.</summary>
    public static string? FindRsiLauncher(string? saved)
    {
        if (!string.IsNullOrEmpty(saved) && File.Exists(saved)) return saved;
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (var root in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                using var key = hive.OpenSubKey(root);
                if (key == null) continue;
                foreach (var name in key.GetSubKeyNames())
                {
                    using var app = key.OpenSubKey(name);
                    if (app?.GetValue("DisplayName") is not string dn || !dn.Contains("RSI Launcher", StringComparison.OrdinalIgnoreCase)) continue;
                    if (app.GetValue("InstallLocation") is string loc)
                    {
                        var exe = Path.Combine(loc, "RSI Launcher.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }
            }
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            foreach (var rel in new[] { @"Program Files\Roberts Space Industries\RSI Launcher", @"Roberts Space Industries\RSI Launcher" })
            {
                var exe = Path.Combine(drive.Name, rel, "RSI Launcher.exe");
                if (File.Exists(exe)) return exe;
            }
        return null;
    }

    public static void StartRsiLauncher(string exe)
    {
        bool wasRunning = IsRsiLauncherRunning();
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
        // When the launcher is already sitting in the system tray, starting it again doesn't show it. Pull its window up.
        if (wasRunning) Task.Delay(1500).ContinueWith(_ => ShowRsiLauncherWindow());
    }

    static void ShowRsiLauncherWindow()
    {
        var pids = Process.GetProcessesByName("RSI Launcher").Select(p => (uint)p.Id).ToHashSet();
        IntPtr best = IntPtr.Zero;
        int bestArea = 0;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var pid);
            if (!pids.Contains(pid) || GetWindowTextLength(h) == 0) return true;
            GetWindowRect(h, out var r);
            int area = (r.Right - r.Left) * (r.Bottom - r.Top);
            if (area > bestArea) { bestArea = area; best = h; }
            return true;
        }, IntPtr.Zero);
        if (best == IntPtr.Zero) return;
        ShowWindow(best, IsIconic(best) ? SW_RESTORE : SW_SHOW);
        SetForegroundWindow(best);
    }

    const int SW_SHOW = 5, SW_RESTORE = 9;
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
}
