namespace SonkinDesktopPets;

/// <summary>All the sheep on the desktop, and the shared view of the windows they stand on.</summary>
internal sealed class SheepFlock : IDisposable
{
    public const int MaxSheep = 12;

    /// <summary>One new sheep in this many is golden.</summary>
    private const int GoldenOdds = 25;

    /// <summary>How long a scan of the desktop's windows is reused for, however many sheep ask.</summary>
    private const long WorldLifetimeMs = 50;

    private readonly Random _rng;
    private readonly List<SheepForm> _sheep = new();
    private Rectangle _bounds;
    private Rectangle[] _workAreas = Array.Empty<Rectangle>();
    private IReadOnlyList<Platform> _world = Array.Empty<Platform>();
    private long? _worldScannedAt;
    private int _scale;
    private bool _background;
    private bool _paused;

    public SheepFlock(Random rng, int scale, bool background)
    {
        _rng = rng;
        _scale = scale;
        _background = background;
    }

    public int Count => _sheep.Count;

    public IEnumerable<SheepBrain> Brains => _sheep.Select(sheep => sheep.Brain);

    public bool Background
    {
        set
        {
            _background = value;
            foreach (var sheep in _sheep) sheep.Background = value;
        }
    }

    public bool Paused
    {
        set
        {
            _paused = value;
            foreach (var sheep in _sheep) sheep.Paused = value;
        }
    }

    /// <summary>
    /// Confines the flock to <paramref name="bounds"/>; <paramref name="workAreas"/> are the
    /// parts of each screen in it that are not taken up by the taskbar.
    /// </summary>
    public void SetArea(Rectangle bounds, Rectangle[] workAreas)
    {
        _bounds = bounds;
        _workAreas = workAreas;
        _worldScannedAt = null;
        foreach (var sheep in _sheep)
        {
            sheep.Brain.SetArea(bounds);
            sheep.SetArea(bounds);
        }
    }

    public void SetScale(int scale)
    {
        _scale = scale;
        _worldScannedAt = null;
        foreach (var sheep in _sheep) sheep.SetScale(scale);
    }

    public void SetCount(int count)
    {
        count = Math.Clamp(count, 0, MaxSheep);
        while (_sheep.Count > count)
        {
            _sheep[^1].Dispose();
            _sheep.RemoveAt(_sheep.Count - 1);
        }

        while (_sheep.Count < count)
        {
            var brain = new SheepBrain(_rng, SheepSprites.SizeAt(_scale), _bounds, World());
            var form = new SheepForm(brain, World, _scale, RollCoat(), _background, (float)_rng.NextDouble() * 4f);
            form.SetArea(_bounds);
            _sheep.Add(form);
            if (_paused) form.Paused = true;
            else form.Show();
        }
    }

    private SheepCoat RollCoat()
    {
        // Any sheep might turn out golden, even the first.
        if (_rng.Next(GoldenOdds) == 0) return SheepCoat.Golden;
        // Every flock has a black sheep. Never the first, so a lone sheep looks like a sheep.
        return _sheep.Count > 0 && _rng.Next(6) == 0 ? SheepCoat.Black : SheepCoat.White;
    }

    private IReadOnlyList<Platform> World()
    {
        long now = Environment.TickCount64;
        if (_worldScannedAt is not { } scannedAt || now - scannedAt >= WorldLifetimeMs)
        {
            var size = SheepSprites.SizeAt(_scale);
            _world = Platforms.Compute(DesktopWindows.Snapshot(), _workAreas, size.Height, size.Width);
            _worldScannedAt = now;
        }

        return _world;
    }

    public void Dispose()
    {
        foreach (var sheep in _sheep) sheep.Dispose();
        _sheep.Clear();
    }
}
