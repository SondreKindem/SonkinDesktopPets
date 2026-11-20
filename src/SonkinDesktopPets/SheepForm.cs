namespace SonkinDesktopPets;

/// <summary>One sheep's overlay window. It steps the sheep's brain and draws it.</summary>
internal sealed class SheepForm : PetOverlayForm
{
    private static readonly string[] Bleats = { "Baa!", "Bææ!", "baaa~", "Mææ!", "BAA!" };
    private static readonly string[] Snore = { "####", "..#.", ".#..", "####" };

    private readonly SheepBrain _brain;
    private readonly Func<IReadOnlyList<Platform>> _world;
    private readonly SheepCoat _coat;
    private readonly float _blinkOffset;
    private readonly SolidBrush _grass = new(SheepSprites.Grass);
    private readonly SolidBrush _sparkle = new(SheepSprites.Sparkle);
    private readonly SolidBrush _water = new(Color.FromArgb(190, 228, 248));
    private Bitmap[] _posesRight = Array.Empty<Bitmap>();
    private Bitmap[] _posesLeft = Array.Empty<Bitmap>();
    private Font? _font;
    private int _scale;

    /// <param name="world">Supplies what there currently is to stand on; asked every frame.</param>
    public SheepForm(SheepBrain brain, Func<IReadOnlyList<Platform>> world, int scale, SheepCoat coat, bool background, float blinkOffset)
        : base(background)
    {
        _brain = brain;
        _world = world;
        _coat = coat;
        _blinkOffset = blinkOffset;
        SetScale(scale);
    }

    public SheepBrain Brain => _brain;

    public void SetScale(int scale)
    {
        foreach (var pose in _posesRight.Concat(_posesLeft)) pose.Dispose();
        _scale = scale;
        _posesRight = SheepSprites.BuildPoses(scale, facingLeft: false, _coat);
        _posesLeft = SheepSprites.BuildPoses(scale, facingLeft: true, _coat);
        _brain.Size = SheepSprites.SizeAt(scale);
        _font?.Dispose();
        _font = new Font("Consolas", scale * 5f, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    protected override void Advance(float dt)
    {
        _brain.SetWorld(_world());
        _brain.Update(dt, Cursor.Position);
    }

    private SheepPose Pose()
    {
        float t = _brain.ModeTime;
        return _brain.Mode switch
        {
            SheepMode.Fall or SheepMode.Jump or SheepMode.Pronk or SheepMode.Startle or SheepMode.Carried => SheepPose.Leap,
            // Landing borrows the lying pose for a moment: it reads as a squash.
            SheepMode.Land or SheepMode.Sit => SheepPose.Sit,
            SheepMode.Sleep => SheepPose.Sleep,
            SheepMode.Graze => (int)(t * 3f) % 2 == 0 ? SheepPose.GrazeA : SheepPose.GrazeB,
            SheepMode.Walk => SheepSprites.WalkCycle[(int)(t * 6f) % SheepSprites.WalkCycle.Length],
            SheepMode.Zoomies or SheepMode.Flee => SheepSprites.WalkCycle[(int)(t * 16f) % SheepSprites.WalkCycle.Length],
            _ => (_brain.Time + _blinkOffset) % 3.7f < 0.15f ? SheepPose.Blink : SheepPose.Stand,
        };
    }

    // Everything below works in screen coordinates.

    private Rectangle SheepRect()
    {
        var size = _brain.Size;
        return new Rectangle((int)(_brain.Position.X - size.Width / 2f), (int)_brain.Position.Y - size.Height, size.Width, size.Height);
    }

    /// <summary>Room above the sheep for a speech bubble or a string of snores.</summary>
    private Rectangle OverheadRect(Rectangle sheep) =>
        new(sheep.X - _scale * 6, sheep.Y - _scale * 18, sheep.Width + _scale * 12, _scale * 18);

    private Rectangle BubbleRect(Rectangle sheep, int width)
    {
        int w = _scale * width, h = _scale * 10;
        return new Rectangle(sheep.X + (sheep.Width - w) / 2, sheep.Y - h - _scale * 3, w, h);
    }

    /// <summary>Converts a column of the right-facing sprite to a screen x, whichever way the sheep faces.</summary>
    private int SpriteX(Rectangle sheep, int column) =>
        sheep.X + (_brain.FacingRight ? column : SheepSprites.Width - 1 - column) * _scale;

    protected override Rectangle ContentBounds()
    {
        var sheep = SheepRect();
        var bounds = sheep;
        if (_brain.Mode is SheepMode.Baa or SheepMode.Sleep or SheepMode.Startle)
        {
            bounds = Rectangle.Union(bounds, OverheadRect(sheep));
        }

        // A golden sheep's sparkles reach a little past its outline.
        int margin = _coat == SheepCoat.Golden ? _scale * 4 : 2;
        return Rectangle.Inflate(bounds, margin, margin);
    }

    /// <summary>
    /// Two twinkles at a time, each popping up somewhere new on the fleece when the last fades.
    /// </summary>
    private void DrawSparkles(Graphics g, Rectangle sheep)
    {
        const float life = 0.5f;
        for (int i = 0; i < 2; i++)
        {
            float clock = (_brain.Time + _blinkOffset) / life + i * 0.5f;
            int number = (int)clock;
            // Scramble the twinkle's number into a spot on the sprite.
            uint spot = (uint)(number * 2 + i) * 2654435761u;
            int x = sheep.X + (int)(spot >> 8) % sheep.Width;
            int y = sheep.Y + (int)(spot >> 20) % sheep.Height;
            int frame = Math.Clamp((int)((clock - number) * Sprites.StarFrames.Length), 0, Sprites.StarFrames.Length - 1);
            foreach (var (dx, dy) in Sprites.StarFrames[frame])
            {
                g.FillRectangle(_sparkle, x + dx * _scale, y + dy * _scale, _scale, _scale);
            }
        }
    }

    protected override void Draw(Graphics g)
    {
        var sheep = SheepRect();
        var poses = _brain.FacingRight ? _posesRight : _posesLeft;
        g.DrawImageUnscaled(poses[(int)Pose()], sheep.Location);
        if (_coat == SheepCoat.Golden) DrawSparkles(g, sheep);

        switch (_brain.Mode)
        {
            case SheepMode.Graze when _brain.Errand == SheepErrand.Drink:
                // Ripples spreading from its nose instead of grass.
                int spread = 1 + (int)(_brain.ModeTime * 3f) % 3;
                int nose = SpriteX(sheep, 23);
                g.FillRectangle(_water, nose - spread * _scale, sheep.Bottom - _scale, (spread * 2 + 1) * _scale, _scale);
                g.FillRectangle(_water, nose, sheep.Bottom - (1 + spread) * _scale, _scale, _scale);
                break;

            case SheepMode.Graze:
                // A tuft of grass at its nose that keeps getting nibbled down and growing back.
                for (int blade = 0; blade < 3; blade++)
                {
                    int height = 1 + (blade + (int)(_brain.ModeTime * 1.5f)) % 3;
                    g.FillRectangle(_grass, SpriteX(sheep, 21 + blade * 2), sheep.Bottom - height * _scale, _scale, height * _scale);
                }

                break;

            case SheepMode.Baa:
                DrawBubble(g, BubbleRect(sheep, 22), _brain.Smitten ? "Baa ♥" : _brain.Errand == SheepErrand.Greet ? "Baa?" : Bleats[_brain.BaaCount % Bleats.Length], _font!, _scale);
                break;

            case SheepMode.Startle:
                DrawBubble(g, BubbleRect(sheep, 8), "!", _font!, _scale);
                break;

            case SheepMode.Sleep:
                DrawSnores(g, sheep);
                break;
        }
    }

    /// <summary>Three little Zs drifting up from the head, growing and fading as they go.</summary>
    private void DrawSnores(Graphics g, Rectangle sheep)
    {
        int direction = _brain.FacingRight ? 1 : -1;
        for (int i = 0; i < 3; i++)
        {
            float phase = (_brain.ModeTime * 0.4f + i / 3f) % 1f;
            float unit = _scale * (0.5f + phase * 0.5f);
            float x = SpriteX(sheep, 21) + direction * phase * _scale * 5f - unit * 2f;
            float y = sheep.Y + _scale * 3 - phase * _scale * 14f;
            using var brush = new SolidBrush(Color.FromArgb((int)(255 * (1f - phase)), 120, 180, 255));
            for (int row = 0; row < Snore.Length; row++)
            {
                for (int column = 0; column < Snore[row].Length; column++)
                {
                    if (Snore[row][column] == '#') g.FillRectangle(brush, x + column * unit, y + row * unit, unit, unit);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var pose in _posesRight.Concat(_posesLeft)) pose.Dispose();
            _grass.Dispose();
            _sparkle.Dispose();
            _water.Dispose();
            _font?.Dispose();
        }

        base.Dispose(disposing);
    }
}
