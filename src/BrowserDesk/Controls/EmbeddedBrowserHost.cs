using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BrowserDesk.Controls;

public sealed class EmbeddedBrowserHost : HwndHost
{
    private IntPtr _hostHandle;
    private IntPtr _browserHandle;

    public bool HasBrowser => _browserHandle != IntPtr.Zero;

    public void Attach(IntPtr browserHandle)
    {
        if (browserHandle == IntPtr.Zero || _hostHandle == IntPtr.Zero)
            return;

        if (_browserHandle != IntPtr.Zero && _browserHandle != browserHandle)
            PostMessage(_browserHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

        _browserHandle = browserHandle;

        var style = GetWindowLongPtr(_browserHandle, GWL_STYLE).ToInt64();
        style &= ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
        style |= WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS;

        SetWindowLongPtr(_browserHandle, GWL_STYLE, new IntPtr(style));
        SetParent(_browserHandle, _hostHandle);

        SetWindowPos(
            _browserHandle,
            IntPtr.Zero,
            0, 0, 1, 1,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);

        ResizeBrowser();
        ShowWindow(_browserHandle, SW_SHOW);
        SetFocus(_browserHandle);
    }

    public void CloseHostedWindow()
    {
        if (_browserHandle == IntPtr.Zero)
            return;

        PostMessage(_browserHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        _browserHandle = IntPtr.Zero;
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hostHandle = CreateWindowEx(
            0,
            "static",
            "",
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 1, 1,
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hostHandle == IntPtr.Zero)
            throw new InvalidOperationException("Browser host window could not be created.");

        return new HandleRef(this, _hostHandle);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        CloseHostedWindow();

        if (hwnd.Handle != IntPtr.Zero)
            DestroyWindow(hwnd.Handle);

        _hostHandle = IntPtr.Zero;
    }

    protected override IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg == WM_SIZE)
            ResizeBrowser();

        return IntPtr.Zero;
    }

    private void ResizeBrowser()
    {
        if (_browserHandle == IntPtr.Zero || _hostHandle == IntPtr.Zero)
            return;

        if (!GetClientRect(_hostHandle, out var rect))
            return;

        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);

        MoveWindow(_browserHandle, 0, 0, width, height, true);
    }

    private const int GWL_STYLE = -16;

    private const long WS_CHILD = 0x40000000L;
    private const long WS_VISIBLE = 0x10000000L;
    private const long WS_POPUP = 0x80000000L;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const long WS_MINIMIZEBOX = 0x00020000L;
    private const long WS_MAXIMIZEBOX = 0x00010000L;
    private const long WS_SYSMENU = 0x00080000L;
    private const long WS_CLIPCHILDREN = 0x02000000L;
    private const long WS_CLIPSIBLINGS = 0x04000000L;

    private const int SW_SHOW = 5;
    private const int WM_SIZE = 0x0005;
    private const int WM_CLOSE = 0x0010;

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int exStyle,
        string className,
        string windowName,
        long style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr after,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
