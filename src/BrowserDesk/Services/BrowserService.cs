using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using BrowserDesk.Models;

namespace BrowserDesk.Services;

public sealed class BrowserService
{
    public string? GetExecutable(BrowserKind browser)
    {
        var exe = browser switch
        {
            BrowserKind.Chrome => "chrome.exe",
            BrowserKind.Firefox => "firefox.exe",
            BrowserKind.Edge => "msedge.exe",
            _ => throw new ArgumentOutOfRangeException(nameof(browser))
        };

        foreach (var key in new[]
        {
            $@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}",
            $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}",
            $@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\{exe}"
        })
        {
            var path = Registry.GetValue(key, "", null) as string;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }

        return GetFallbackPaths(browser).FirstOrDefault(File.Exists);
    }

    public string GetVersionText(BrowserKind browser)
    {
        var path = GetExecutable(browser);
        if (path is null) return "نصب نیست";

        try
        {
            return FileVersionInfo.GetVersionInfo(path).ProductVersion ?? "نصب شده";
        }
        catch
        {
            return "نصب شده";
        }
    }

    public async Task OpenAsync(BrowserKind browser, string url, bool tileLeft)
    {
        var path = GetExecutable(browser)
            ?? throw new FileNotFoundException($"مرورگر {browser} روی ویندوز پیدا نشد.");

        var info = new ProcessStartInfo(path) { UseShellExecute = false };
        info.ArgumentList.Add(browser == BrowserKind.Firefox ? "-new-window" : "--new-window");
        info.ArgumentList.Add(url);
        Process.Start(info);

        if (!tileLeft) return;

        var processName = browser switch
        {
            BrowserKind.Chrome => "chrome",
            BrowserKind.Firefox => "firefox",
            BrowserKind.Edge => "msedge",
            _ => ""
        };

        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(200);

            var window = Process.GetProcessesByName(processName)
                .Select(p => p.MainWindowHandle)
                .FirstOrDefault(h => h != IntPtr.Zero);

            if (window != IntPtr.Zero)
            {
                Tile(window, true);
                return;
            }
        }
    }

    public void TileRight(IntPtr hwnd) => Tile(hwnd, false);

    private static IEnumerable<string> GetFallbackPaths(BrowserKind browser)
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return browser switch
        {
            BrowserKind.Chrome =>
            [
                Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(pfx86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe")
            ],
            BrowserKind.Firefox =>
            [
                Path.Combine(pf, "Mozilla Firefox", "firefox.exe"),
                Path.Combine(pfx86, "Mozilla Firefox", "firefox.exe")
            ],
            BrowserKind.Edge =>
            [
                Path.Combine(pfx86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe")
            ],
            _ => []
        };
    }

    private static void Tile(IntPtr hwnd, bool left)
    {
        ShowWindow(hwnd, 9);
        var monitor = MonitorFromWindow(hwnd, 2);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref mi)) return;

        var width = mi.rcWork.Right - mi.rcWork.Left;
        var height = mi.rcWork.Bottom - mi.rcWork.Top;
        var half = width / 2;
        var x = left ? mi.rcWork.Left : mi.rcWork.Left + half;

        SetWindowPos(hwnd, IntPtr.Zero, x, mi.rcWork.Top, half, height, 0x0004 | 0x0040);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}
