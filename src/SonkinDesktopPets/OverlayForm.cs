using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SonkinDesktopPets;

/// <summary>
/// A borderless, always-on-top, click-through window. Each frame it is moved and resized to
/// just cover the cat, its rainbow and its stars, and given a freshly drawn per-pixel-alpha
/// image, so the cost is proportional to what is drawn rather than to the size of the desktop.
/// </summary>
internal sealed class OverlayForm : Form
{
    private const int WM_DPICHANGED = 0x02E0;
    private const int WS_EX_TOPMOST = 0x8;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_NOACTIVATE = 0x8000000;

    private bool _background;
    private double _lastSentToBottom;

    private readonly NyanBrain _brain;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly SolidBrush[] _rainbow = Sprites.Rainbow.Select(c => new SolidBrush(c)).ToArray();
    private readonly SolidBrush[] _starBrushes = Sprites.StarTints.Select(c => new SolidBrush(c)).ToArray();
    private static readonly string[] Lyrics = { "NYAN!", "nyan nyan", "nyan~", "NYAN NYAN!", "nya-nyan" };

    private readonly SolidBrush[] _crumbBrushes = Sprites.CrumbTints.Select(c => new SolidBrush(c)).ToArray();
    private readonly StringFormat _centred = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    private Font? _lyricFont;
    private readonly SolidBrush _paper = new(Color.FromArgb(255, 250, 200));
    private readonly SolidBrush _ink = new(Color.FromArgb(60, 60, 60));
    private readonly Surface _surface = new();

    private Bitmap[] _framesRight = Array.Empty<Bitmap>();
    private Bitmap[] _framesLeft = Array.Empty<Bitmap>();
    private Rectangle _area;
    private int _scale;
    private double _lastTick;

    public OverlayForm(NyanBrain brain, int scale, bool background)
    {
        _brain = brain;
        _background = background;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        TopMost = !background;
        SetScale(scale);

        _timer.Tick += (_, _) => Step();
        _timer.Start();
    }

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

    /// <summary>True to fly behind every other window, just above the wallpaper and desktop icons.</summary>
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

    private void SendToBottom()
    {
        if (IsHandleCreated) WindowStack.SendToBottom(Handle);
        _lastSentToBottom = _clock.Elapsed.TotalSeconds;
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

    /// <summary>Sets the screen area the cat may be drawn in; anything outside it is clipped.</summary>
    public void SetArea(Rectangle area) => _area = area;

    public void SetScale(int scale)
    {
        foreach (var frame in _framesRight.Concat(_framesLeft)) frame.Dispose();
        _scale = scale;
        _framesRight = Sprites.BuildFrames(scale, facingLeft: false);
        _framesLeft = Sprites.BuildFrames(scale, facingLeft: true);
        _brain.CatSize = Sprites.SizeAt(scale);
        _lyricFont?.Dispose();
        _lyricFont = new Font("Consolas", scale * 5f, FontStyle.Bold, GraphicsUnit.Pixel);
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

    private void Step()
    {
        double now = _clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(now - _lastTick, 0.05);
        _lastTick = now;

        _brain.Update(dt, Cursor.Position);
        if (_brain.CursorGrab is { } grab) Cursor.Position = grab;

        Present();

        // Showing the window, or another app shuffling the stack, can lift it; push it back down.
        if (_background && now - _lastSentToBottom > 0.5) SendToBottom();
    }

    private void Present()
    {
        var bounds = Rectangle.Intersect(ContentBounds(), _area);
        // Nothing visible (e.g. mid-dash, off the edge): keep a single transparent pixel.
        if (bounds.IsEmpty) bounds = new Rectangle(_area.Location, new Size(1, 1));

        var g = _surface.Begin(bounds.Size);
        g.TranslateTransform(-bounds.X, -bounds.Y);
        Draw(g);
        g.ResetTransform();
        _surface.Present(Handle, bounds);
    }

    // Everything below works in screen coordinates.

    private Rectangle CatRect()
    {
        var size = _brain.CatSize;
        float bob = MathF.Sin(_brain.Time * 8f) * _scale * 0.75f;
        return new Rectangle(
            (int)(_brain.Position.X - size.Width / 2f),
            (int)(_brain.Position.Y - size.Height / 2f + bob),
            size.Width,
            size.Height);
    }

    private Rectangle NoteRect(Rectangle cat)
    {
        int w = _scale * 14, h = _scale * 11;
        return new Rectangle(cat.X + (cat.Width - w) / 2, cat.Bottom + _scale * 2, w, h);
    }

    private Rectangle BubbleRect(Rectangle cat)
    {
        int w = _scale * 34, h = _scale * 10;
        return new Rectangle(cat.X + (cat.Width - w) / 2, cat.Y - h - _scale * 3, w, h);
    }

    private Rectangle CrumbRect(Crumb crumb) => new((int)crumb.Position.X, (int)crumb.Position.Y, _scale, _scale);

    private RectangleF TrailRect(TrailSegment segment)
    {
        float length = _brain.CatSize.Width / 4f + 2f;
        float remaining = NyanBrain.TrailLife - (_brain.Time - segment.Born);
        float height = 18f * _scale * Math.Clamp(remaining / 0.3f, 0f, 1f);
        // Alternate segments ride high and low, and swap over time: the classic rainbow wobble.
        bool high = (segment.Index + (int)(_brain.Time * 6f)) % 2 == 0;
        float centre = segment.Position.Y + (Sprites.BodyCentreRow - Sprites.Height / 2f) * _scale + (high ? -_scale : _scale);
        return new RectangleF(segment.Position.X - length / 2f, centre - height / 2f, length, height);
    }

    private Rectangle StarRect(Star star)
    {
        int reach = _scale * 4;
        return new Rectangle((int)star.Position.X - reach, (int)star.Position.Y - reach, reach * 2, reach * 2);
    }

    private Rectangle ContentBounds()
    {
        var cat = CatRect();
        var bounds = cat;
        if (_brain.CarryingNote) bounds = Rectangle.Union(bounds, NoteRect(cat));
        if (_brain.Mode == NyanMode.Sing) bounds = Rectangle.Union(bounds, BubbleRect(cat));
        foreach (var crumb in _brain.Crumbs) bounds = Rectangle.Union(bounds, CrumbRect(crumb));
        foreach (var segment in _brain.Trail) bounds = Rectangle.Union(bounds, Rectangle.Ceiling(TrailRect(segment)));
        foreach (var star in _brain.Stars) bounds = Rectangle.Union(bounds, StarRect(star));
        return Rectangle.Inflate(bounds, 2, 2);
    }

    private void Draw(Graphics g)
    {
        foreach (var segment in _brain.Trail)
        {
            var rect = TrailRect(segment);
            float stripe = rect.Height / _rainbow.Length;
            if (stripe < 0.5f) continue;
            for (int i = 0; i < _rainbow.Length; i++)
            {
                g.FillRectangle(_rainbow[i], rect.X, rect.Y + i * stripe, rect.Width, stripe + 1f);
            }
        }

        foreach (var star in _brain.Stars)
        {
            float age = (_brain.Time - star.Born) / NyanBrain.StarLife;
            int frame = Math.Clamp((int)(age * Sprites.StarFrames.Length), 0, Sprites.StarFrames.Length - 1);
            int x = (int)star.Position.X, y = (int)star.Position.Y;
            foreach (var (dx, dy) in Sprites.StarFrames[frame])
            {
                g.FillRectangle(_starBrushes[star.Tint], x + dx * _scale, y + dy * _scale, _scale, _scale);
            }
        }

        var cat = CatRect();
        if (_brain.CarryingNote)
        {
            var note = NoteRect(cat);
            g.FillRectangle(_ink, note.X + note.Width / 2 - _scale / 2, cat.Bottom - _scale, _scale, note.Y - cat.Bottom + _scale);
            g.FillRectangle(_ink, note);
            g.FillRectangle(_paper, Rectangle.Inflate(note, -_scale, -_scale));
            for (int line = 0; line < 3; line++)
            {
                g.FillRectangle(_ink, note.X + _scale * 3, note.Y + _scale * (3 + line * 2), note.Width - _scale * 6, _scale);
            }
        }

        foreach (var crumb in _brain.Crumbs) g.FillRectangle(_crumbBrushes[crumb.Tint], CrumbRect(crumb));

        var frames = _brain.FacingRight ? _framesRight : _framesLeft;
        g.DrawImageUnscaled(frames[(int)(_brain.Time * 12f) % Sprites.FrameCount], cat.Location);

        if (_brain.Mode == NyanMode.Sing)
        {
            var bubble = BubbleRect(cat);
            g.FillRectangle(_ink, bubble);
            g.FillRectangle(Brushes.White, Rectangle.Inflate(bubble, -_scale, -_scale));
            // The little pointer from the bubble down to the cat.
            g.FillRectangle(_ink, bubble.X + bubble.Width / 2 - _scale, bubble.Bottom, _scale * 2, _scale * 2);
            g.DrawString(Lyrics[_brain.SingCount % Lyrics.Length], _lyricFont!, _ink, bubble, _centred);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _surface.Dispose();
            foreach (var frame in _framesRight.Concat(_framesLeft)) frame.Dispose();
            foreach (var brush in _rainbow.Concat(_starBrushes).Concat(_crumbBrushes)) brush.Dispose();
            _centred.Dispose();
            _lyricFont?.Dispose();
            _paper.Dispose();
            _ink.Dispose();
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
            // Round up generously so a slowly growing rainbow doesn't reallocate every frame.
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
