namespace SonkinDesktopPets;

/// <summary>The cat's overlay: draws the cat, its rainbow, its stars and whatever it is carrying.</summary>
internal sealed class OverlayForm : PetOverlayForm
{
    private static readonly string[] Lyrics = { "NYAN!", "nyan nyan", "nyan~", "NYAN NYAN!", "nya-nyan" };

    private readonly NyanBrain _brain;
    private readonly SolidBrush[] _rainbow = Sprites.Rainbow.Select(c => new SolidBrush(c)).ToArray();
    private readonly SolidBrush[] _starBrushes = Sprites.StarTints.Select(c => new SolidBrush(c)).ToArray();
    private readonly SolidBrush[] _crumbBrushes = Sprites.CrumbTints.Select(c => new SolidBrush(c)).ToArray();
    private readonly SolidBrush _paper = new(Color.FromArgb(255, 250, 200));
    private Font? _lyricFont;

    private Bitmap[] _framesRight = Array.Empty<Bitmap>();
    private Bitmap[] _framesLeft = Array.Empty<Bitmap>();
    private int _scale;

    public OverlayForm(NyanBrain brain, int scale, bool background)
        : base(background)
    {
        _brain = brain;
        SetScale(scale);
    }

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

    protected override void Advance(float dt)
    {
        _brain.Update(dt, Cursor.Position);
        if (_brain.CursorGrab is { } grab) Cursor.Position = grab;
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

    protected override Rectangle ContentBounds()
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

    protected override void Draw(Graphics g)
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
            g.FillRectangle(Ink, note.X + note.Width / 2 - _scale / 2, cat.Bottom - _scale, _scale, note.Y - cat.Bottom + _scale);
            g.FillRectangle(Ink, note);
            g.FillRectangle(_paper, Rectangle.Inflate(note, -_scale, -_scale));
            for (int line = 0; line < 3; line++)
            {
                g.FillRectangle(Ink, note.X + _scale * 3, note.Y + _scale * (3 + line * 2), note.Width - _scale * 6, _scale);
            }
        }

        foreach (var crumb in _brain.Crumbs) g.FillRectangle(_crumbBrushes[crumb.Tint], CrumbRect(crumb));

        var frames = _brain.FacingRight ? _framesRight : _framesLeft;
        g.DrawImageUnscaled(frames[(int)(_brain.Time * 12f) % Sprites.FrameCount], cat.Location);

        if (_brain.Mode == NyanMode.Sing)
        {
            DrawBubble(g, BubbleRect(cat), Lyrics[_brain.SingCount % Lyrics.Length], _lyricFont!, _scale);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var frame in _framesRight.Concat(_framesLeft)) frame.Dispose();
            foreach (var brush in _rainbow.Concat(_starBrushes).Concat(_crumbBrushes)) brush.Dispose();
            _lyricFont?.Dispose();
            _paper.Dispose();
        }

        base.Dispose(disposing);
    }
}
