using System.Runtime.InteropServices;

namespace SonkinDesktopPets;

/// <summary>Keeps windows at the bottom of the window stack, just above the desktop.</summary>
internal static class WindowStack
{
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_WINDOWPOSCHANGED = 0x0047;

    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
    private static readonly IntPtr HWND_BOTTOM = new(1);

    /// <summary>Windows that belong underneath everything else that is sent to the bottom: the scenery.</summary>
    private static readonly List<IntPtr> Floor = new();

    public static void AddFloor(IntPtr window) => Floor.Add(window);

    public static void RemoveFloor(IntPtr window) => Floor.Remove(window);

    public static void SendToBottom(IntPtr window)
    {
        Sink(window);
        SinkFloor(window);
    }

    /// <summary>Puts the floor windows back below one that has just landed on the bottom.</summary>
    public static void SinkFloor(IntPtr above)
    {
        foreach (var floor in Floor)
        {
            if (floor != above) Sink(floor);
        }
    }

    private static void Sink(IntPtr window) =>
        SetWindowPos(window, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

    /// <summary>
    /// Call from WndProc on <see cref="WM_WINDOWPOSCHANGING"/> to rewrite the pending change so
    /// the window lands at the bottom whatever was asked for, e.g. when the user clicks on it.
    /// </summary>
    public static void ForceBottom(ref Message message)
    {
        var position = Marshal.PtrToStructure<WINDOWPOS>(message.LParam);
        position.InsertAfter = HWND_BOTTOM;
        position.Flags &= ~SWP_NOZORDER;
        Marshal.StructureToPtr(position, message.LParam, false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr Window, InsertAfter;
        public int X, Y, Width, Height;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
