using System.Diagnostics;
using System.IO;
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
        if (path is null)
            return "نصب نیست";

        try
        {
            return FileVersionInfo.GetVersionInfo(path).ProductVersion ?? "نصب شده";
        }
        catch
        {
            return "نصب شده";
        }
    }

    public async Task<IntPtr> OpenEmbeddedWindowAsync(BrowserKind browser, string url)
    {
        var executable = GetExecutable(browser)
            ?? throw new FileNotFoundException($"مرورگر {GetDisplayName(browser)} روی ویندوز نصب نیست.");

        var before = EnumerateBrowserWindows(browser).ToHashSet();

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false
        };

        if (browser == BrowserKind.Firefox)
            startInfo.ArgumentList.Add("-new-window");
        else
            startInfo.ArgumentList.Add("--new-window");

        startInfo.ArgumentList.Add(url);
        Process.Start(startInfo);

        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(200);

            var handle = EnumerateBrowserWindows(browser)
                .FirstOrDefault(h => !before.Contains(h));

            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_RESTORE);
                return handle;
            }
        }

        throw new InvalidOperationException(
            $"پنجره جدید {GetDisplayName(browser)} پیدا نشد. مرورگر را ببندید و دوباره امتحان کنید.");
    }

    public bool IsInstalled(BrowserKind browser) => GetExecutable(browser) is not null;

    private static string GetDisplayName(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => "Google Chrome",
        BrowserKind.Firefox => "Mozilla Firefox",
        BrowserKind.Edge => "Microsoft Edge",
        _ => browser.ToString()
    };

    private static IReadOnlyList<IntPtr> EnumerateBrowserWindows(BrowserKind browser)
    {
        var processName = browser switch
        {
            BrowserKind.Chrome => "chrome",
            BrowserKind.Firefox => "firefox",
            BrowserKind.Edge => "msedge",
            _ => ""
        };

        var handles = new List<IntPtr>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            GetWindowThreadProcessId(hwnd, out var processId);

            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    handles.Add(hwnd);
            }
            catch
            {
                // Process can exit while windows are being enumerated.
            }

            return true;
        }, IntPtr.Zero);

        return handles;
    }

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

    private const int SW_RESTORE = 9;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);
}
