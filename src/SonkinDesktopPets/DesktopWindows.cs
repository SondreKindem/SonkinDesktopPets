using System.Runtime.InteropServices;

namespace SonkinDesktopPets;

/// <summary>Lists the real windows on the desktop, for the sheep to climb on.</summary>
internal static class DesktopWindows
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;

    private static readonly string[] ShellClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
    private static readonly EnumWindowsProc Callback = Collect;
    private static readonly char[] ClassName = new char[64];
    private static readonly uint OwnProcess = (uint)Environment.ProcessId;
    private static List<DesktopWindow> _collecting = new();

    /// <summary>
    /// The visible, ordinary windows of other programs, topmost first. Call from the UI thread only.
    /// </summary>
    public static List<DesktopWindow> Snapshot()
    {
        _collecting = new List<DesktopWindow>();
        EnumWindows(Callback, IntPtr.Zero);
        return _collecting;
    }

    private static bool Collect(IntPtr window, IntPtr parameter)
    {
        if (!IsWindowVisible(window) || IsIconic(window)) return true;

        // Click-through overlays and tool windows are neither something to stand on nor in the way.
        int style = GetWindowLong(window, GWL_EXSTYLE);
        if ((style & (WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW)) != 0) return true;

        // Windows on other virtual desktops and suspended store apps are "visible" but cloaked.
        if (DwmGetWindowAttribute(window, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;

        GetWindowThreadProcessId(window, out uint process);
        if (process == OwnProcess) return true;

        int length = GetClassName(window, ClassName, ClassName.Length);
        var name = ClassName.AsSpan(0, Math.Max(0, length));
        foreach (string shell in ShellClasses)
        {
            if (name.SequenceEqual(shell)) return true;
        }

        // The frame bounds leave out the invisible resize border that GetWindowRect includes.
        if (DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>()) != 0
            && !GetWindowRect(window, out rect))
        {
            return true;
        }

        var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        if (bounds.Width <= 0 || bounds.Height <= 0) return true;

        _collecting.Add(new DesktopWindow(window.ToInt64(), bounds, IsZoomed(window)));
        return true;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, [Out] char[] name, int capacity);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out RECT rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT value, int size);
}
