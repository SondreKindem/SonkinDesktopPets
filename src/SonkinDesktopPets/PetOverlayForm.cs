using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SonkinDesktopPets;

/// <summary>
/// A borderless, always-on-top, click-through window for one pet. Each frame it is moved and
/// resized to just cover what the pet draws, and given a freshly drawn per-pixel-alpha image,
/// so the cost is proportional to what is drawn rather than to the size of the desktop.
/// </summary>
internal abstract class PetOverlayForm : Form
{
    private const int WM_DPICHANGED = 0x02E0;
    private const int WS_EX_TOPMOST = 0x8;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_NOACTIVATE = 0x8000000;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Surface _surface = new();
    private readonly StringFormat _centred = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    private Rectangle _area;
    private bool _background;
    private double _lastSentToBottom;
    private double _lastTick;

    protected PetOverlayForm(bool background)
    {
        _background = background;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        TopMost = !background;

        _timer.Tick += (_, _) => Step();
        _timer.Start();
    }

    protected SolidBrush Ink { get; } = new(Color.FromArgb(60, 60, 60));

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            if (!_background) cp.ExStyle |= WS_EX_TOPMOST;
            return cp;
        }
    }

    /// <summary>True to stay behind every other window, just above the wallpaper and desktop icons.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Background
    {
        get => _background;
        set
        {
            _background = value;
            TopMost = !value;
            if (value) SendToBottom();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Paused
    {
        get => !_timer.Enabled;
        set
        {
            if (value == Paused) return;
            if (value)
            {
                _timer.Stop();
                Hide();
            }
            else
            {
                _lastTick = _clock.Elapsed.TotalSeconds;
                Show();
                _timer.Start();
            }
        }
    }

    /// <summary>Sets the screen area the pet may be drawn in; anything outside it is clipped.</summary>
    public void SetArea(Rectangle area) => _area = area;

    /// <summary>Moves the pet on by <paramref name="dt"/> seconds.</summary>
    protected abstract void Advance(float dt);

    /// <summary>The screen rectangle that <see cref="Draw"/> will touch this frame.</summary>
    protected abstract Rectangle ContentBounds();

    /// <summary>Draws the pet, in screen coordinates.</summary>
    protected abstract void Draw(Graphics g);

    /// <summary>A pixel-art speech bubble with a little pointer down to the speaker.</summary>
    protected void DrawBubble(Graphics g, Rectangle bubble, string text, Font font, int scale)
    {
        g.FillRectangle(Ink, bubble);
        g.FillRectangle(Brushes.White, Rectangle.Inflate(bubble, -scale, -scale));
        g.FillRectangle(Ink, bubble.X + bubble.Width / 2 - scale, bubble.Bottom, scale * 2, scale * 2);
        g.DrawString(text, font, Ink, bubble, _centred);
    }

    protected override void WndProc(ref Message m)
    {
        // The window is placed in physical pixels every frame; don't let Windows resize it
        // when it crosses onto a monitor with a different DPI.
        if (m.Msg == WM_DPICHANGED) return;
        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The window's pixels come from Present(), never from WM_PAINT.
    }

    private void SendToBottom()
    {
        if (IsHandleCreated) WindowStack.SendToBottom(Handle);
        _lastSentToBottom = _clock.Elapsed.TotalSeconds;
    }

    private void Step()
    {
        double now = _clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(now - _lastTick, 0.05);
        _lastTick = now;

        Advance(dt);
        Present();

        // Showing the window, or another app shuffling the stack, can lift it; push it back down.
        if (_background && now - _lastSentToBottom > 0.5) SendToBottom();
    }

    private void Present()
    {
        var bounds = Rectangle.Intersect(ContentBounds(), _area);
        // Nothing visible (e.g. off the edge of the screen): keep a single transparent pixel.
        if (bounds.IsEmpty) bounds = new Rectangle(_area.Location, new Size(1, 1));

        var g = _surface.Begin(bounds.Size);
        g.TranslateTransform(-bounds.X, -bounds.Y);
        Draw(g);
        g.ResetTransform();
        _surface.Present(Handle, bounds);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _surface.Dispose();
            _centred.Dispose();
            Ink.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// A reusable premultiplied-ARGB pixel buffer that GDI+ can draw into and that
    /// UpdateLayeredWindow can show. It only ever grows, so steady state allocates nothing.
    /// </summary>
    private sealed class Surface : IDisposable
    {
        private const int ULW_ALPHA = 2;
        private const byte AC_SRC_ALPHA = 1;

        private IntPtr _dc;
        private IntPtr _dib;
        private IntPtr _previousObject;
        private Bitmap? _bitmap;
        private Graphics? _graphics;
        private Size _capacity;
        private Size _used;

        /// <summary>Returns a cleared canvas of at least <paramref name="size"/>, origin at its top-left.</summary>
        public Graphics Begin(Size size)
        {
            if (size.Width > _capacity.Width || size.Height > _capacity.Height) Allocate(size);
            _used = size;

            var g = _graphics!;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.FillRectangle(Brushes.Transparent, 0, 0, size.Width, size.Height);
            g.CompositingMode = CompositingMode.SourceOver;
            return g;
        }

        /// <summary>Moves the window to <paramref name="bounds"/> and shows what was drawn since Begin.</summary>
        public void Present(IntPtr window, Rectangle bounds)
        {
            _graphics!.Flush();
            var destination = new POINT { X = bounds.X, Y = bounds.Y };
            var size = new SIZE { Width = _used.Width, Height = _used.Height };
            var source = default(POINT);
            var blend = new BLENDFUNCTION { SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            UpdateLayeredWindow(window, IntPtr.Zero, ref destination, ref size, _dc, ref source, 0, ref blend, ULW_ALPHA);
        }

        private void Allocate(Size needed)
        {
            // Round up generously so slowly growing content doesn't reallocate every frame.
            var capacity = new Size(
                Math.Max(_capacity.Width, (needed.Width + 255) / 256 * 256),
                Math.Max(_capacity.Height, (needed.Height + 255) / 256 * 256));
            Release();

            var header = new BITMAPINFOHEADER
            {
                Size = Marshal.SizeOf<BITMAPINFOHEADER>(),
                Width = capacity.Width,
                Height = -capacity.Height, // negative: rows run top-down, matching GDI+
                Planes = 1,
                BitCount = 32,
            };
            _dc = CreateCompatibleDC(IntPtr.Zero);
            _dib = CreateDIBSection(_dc, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (_dib == IntPtr.Zero) throw new Win32Exception();
            _previousObject = SelectObject(_dc, _dib);
            _bitmap = new Bitmap(capacity.Width, capacity.Height, capacity.Width * 4, PixelFormat.Format32bppPArgb, bits);
            _graphics = Graphics.FromImage(_bitmap);
            _capacity = capacity;
        }

        private void Release()
        {
            _graphics?.Dispose();
            _bitmap?.Dispose();
            _graphics = null;
            _bitmap = null;
            if (_dc != IntPtr.Zero)
            {
                SelectObject(_dc, _previousObject);
                DeleteObject(_dib);
                DeleteDC(_dc);
                _dc = _dib = IntPtr.Zero;
            }
        }

        public void Dispose() => Release();

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int Width, Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int Size, Width, Height;
            public short Planes, BitCount;
            public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateLayeredWindow(
            IntPtr window, IntPtr destinationDc, ref POINT destination, ref SIZE size,
            IntPtr sourceDc, ref POINT source, int colourKey, ref BLENDFUNCTION blend, int flags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(
            IntPtr dc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr dc);
    }
}
