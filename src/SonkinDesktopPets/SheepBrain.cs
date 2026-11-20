namespace SonkinDesktopPets;

public enum SheepMode
{
    Fall,
    Land,
    Idle,
    Walk,
    Graze,
    Sit,
    Sleep,
    Baa,
    Jump,
    Pronk,
    Zoomies,
    Startle,
    Flee,
    Carried,
}

/// <summary>Something a sheep goes out of its way to do at a spot in the scenery.</summary>
public enum SheepErrand
{
    None,

    /// <summary>Drink from a pond.</summary>
    Drink,

    /// <summary>Eat something better than grass.</summary>
    Munch,

    /// <summary>Lie down in the shade.</summary>
    Rest,

    /// <summary>Sit by a fire.</summary>
    Warm,

    /// <summary>Bleat at another animal.</summary>
    Greet,
}

/// <summary>A spot on the ground, in screen coordinates, with something there for a sheep to do.</summary>
public readonly record struct Attraction(SheepErrand Errand, PointF Spot);

/// <summary>
/// A sheep's behaviour, in screen coordinates. It lives on <see cref="Platform"/>s: the ground
/// at the bottom of each screen and the top edges of windows. The host feeds it the current
/// platforms and the cursor position; it knows nothing about real windows.
/// </summary>
public sealed class SheepBrain
{
    private const float LandDuration = 0.25f;
    private const float BaaDuration = 1.6f;
    private const float StartleCooldown = 4f;

    private readonly Random _rng;
    private IReadOnlyList<Platform>? _source;
    private Platform[] _platforms = Array.Empty<Platform>();
    private Platform? _platform;
    private (long Id, float X)? _jump;
    private Attraction? _goingTo;
    private float _targetX;
    private float _duration;
    private float _fallSpeed;
    private float _startleCooldown;
    private int _repeats;
    private int _direction = 1;
    private float _scareX;
    private PointF? _cat;
    private bool _catSwooping;

    private PointF _arcFrom, _arcTo;
    private float _arcVx, _arcVy, _arcDuration;
    private Platform _landing;

    public SheepBrain(Random rng, Size size, Rectangle bounds, IReadOnlyList<Platform>? platforms = null)
    {
        _rng = rng;
        Size = size;
        Bounds = bounds;
        FacingRight = rng.Next(2) == 0;
        SetWorld(platforms ?? Array.Empty<Platform>());
        DropIn();
    }

    public Size Size { get; set; }

    /// <summary>The area the sheep lives in.</summary>
    public Rectangle Bounds { get; private set; }

    /// <summary>Where the sheep's feet are: the bottom-centre of the sprite.</summary>
    public PointF Position { get; private set; }

    public bool FacingRight { get; private set; }

    public SheepMode Mode { get; private set; }

    public float Time { get; private set; }

    /// <summary>Seconds spent in the current <see cref="Mode"/>.</summary>
    public float ModeTime { get; private set; }

    /// <summary>What the sheep is standing on, or null while it is in the air.</summary>
    public Platform? Standing => _platform;

    /// <summary>How many times the sheep has bleated; the host uses it to vary the sound.</summary>
    public int BaaCount { get; private set; }

    /// <summary>True while the current bleat is an answer to the cat's singing.</summary>
    public bool Smitten { get; private set; }

    /// <summary>
    /// Set by the host: things on the ground worth walking over to. A sheep on that ground
    /// visits one now and then.
    /// </summary>
    public IReadOnlyList<Attraction> Attractions { get; set; } = Array.Empty<Attraction>();

    /// <summary>What the sheep has come to an attraction to do, while it is doing it.</summary>
    public SheepErrand Errand { get; private set; }

    /// <summary>The top-centre of the sheep: where the cat takes hold of it.</summary>
    public PointF Top => new(Position.X, Position.Y - Size.Height);

    private bool Calm => _platform is not null
        && Mode is SheepMode.Idle or SheepMode.Walk or SheepMode.Graze or SheepMode.Sit or SheepMode.Sleep or SheepMode.Baa;

    /// <summary>
    /// Tells the sheep where the cat is (its centre), or null if there is no cat about, and
    /// whether it is flying fast enough to give a sheep a fright.
    /// </summary>
    public void SeeCat(PointF? cat, bool swooping)
    {
        _cat = cat;
        _catSwooping = swooping;
    }

    /// <summary>The cat is singing to this sheep. If it is at leisure, it bleats back.</summary>
    public void HearSong()
    {
        if (!Calm) return;
        StartBaa();
        Smitten = true;
    }

    /// <summary>
    /// The cat has hold of the sheep by the top of its fleece, at <paramref name="grip"/>.
    /// Call every frame while it is being carried, then <see cref="Release"/>.
    /// </summary>
    public void CarryTo(PointF grip)
    {
        if (Mode != SheepMode.Carried) Enter(SheepMode.Carried);
        _platform = null;
        if (MathF.Abs(grip.X - Position.X) > 1f) FacingRight = grip.X > Position.X;
        Position = new PointF(grip.X, grip.Y + Size.Height);
    }

    /// <summary>Lets go of a carried sheep, which drops onto whatever is below.</summary>
    public void Release()
    {
        if (Mode == SheepMode.Carried) StartFall();
    }

    private float WalkSpeed => Size.Width * 0.7f;
    private float RunSpeed => Size.Width * 4.5f;
    private float Gravity => Size.Width * 30f;

    public void SetArea(Rectangle bounds)
    {
        Bounds = bounds;
        RebuildWorld();
    }

    /// <summary>Tells the sheep what there is to stand on right now. Cheap to call every frame.</summary>
    public void SetWorld(IReadOnlyList<Platform> platforms)
    {
        if (ReferenceEquals(platforms, _source)) return;
        _source = platforms;
        RebuildWorld();
    }

    private void RebuildWorld()
    {
        var platforms = (_source ?? Array.Empty<Platform>()).ToList();
        // Always somewhere to land, even if the host has nothing to offer.
        if (!platforms.Any(p => p.IsGround))
        {
            platforms.Add(new Platform(-1, Bounds.Left, Bounds.Right, Bounds.Bottom, Bounds.Left));
        }

        _platforms = platforms.ToArray();
    }

    public void Update(float dt, Point cursor)
    {
        Time += dt;
        ModeTime += dt;
        _startleCooldown = MathF.Max(0, _startleCooldown - dt);

        if (_platform is not null) KeepFooting();

        switch (Mode)
        {
            case SheepMode.Fall:
                UpdateFall(dt);
                break;

            case SheepMode.Land:
                if (ModeTime >= LandDuration) ChooseNext();
                break;

            case SheepMode.Idle:
                // Keep an eye on the cat if it is close by, otherwise on the cursor.
                if (_cat is { } near && MathF.Abs(near.X - Position.X) < Size.Width * 8f)
                {
                    if (MathF.Abs(near.X - Position.X) > Size.Width / 2f) FacingRight = near.X > Position.X;
                }
                else if (Bounds.Contains(cursor) && MathF.Abs(cursor.X - Position.X) > Size.Width / 2f)
                {
                    FacingRight = cursor.X > Position.X;
                }

                if (ModeTime >= _duration) ChooseNext();
                break;

            case SheepMode.Graze:
            case SheepMode.Baa:
                if (ModeTime >= _duration) ChooseNext();
                break;

            case SheepMode.Sit:
                if (ModeTime < _duration) break;
                if (_rng.NextDouble() < 0.4) StartTimed(SheepMode.Sleep, 10f, 25f);
                else ChooseNext();
                break;

            case SheepMode.Sleep:
                if (ModeTime >= _duration) StartTimed(SheepMode.Idle, 1f, 2f);
                break;

            case SheepMode.Walk:
                if (!WalkToward(_targetX, WalkSpeed, dt)) break;
                if (_jump is { } jump) TakeOff(jump.Id, jump.X);
                else if (_goingTo is { } attraction) Arrive(attraction);
                else ChooseNext();
                break;

            case SheepMode.Zoomies:
                if (!WalkToward(_targetX, RunSpeed, dt)) break;
                if (--_repeats > 0) AimZoomies();
                else ChooseNext();
                break;

            case SheepMode.Flee:
                if (WalkToward(_targetX, RunSpeed, dt)) StartTimed(SheepMode.Idle, 1f, 2.5f);
                break;

            case SheepMode.Jump:
                if (FlyArc()) Enter(SheepMode.Land);
                break;

            case SheepMode.Pronk:
                if (!FlyArc()) break;
                if (--_repeats > 0) Hop();
                else ChooseNext();
                break;

            case SheepMode.Startle:
                if (FlyArc()) StartFlee();
                break;

            case SheepMode.Carried:
                // Dangling from the cat; the host moves it with CarryTo.
                break;
        }

        if (Calm && _startleCooldown <= 0)
        {
            var body = BodyRect;
            if (body.Contains(cursor))
            {
                Spook(cursor.X);
            }
            else if (_catSwooping && _cat is { } cat
                && MathF.Abs(cat.X - Position.X) < Size.Width && MathF.Abs(cat.Y - (body.Top + body.Height / 2f)) < Size.Height)
            {
                Spook(cat.X);
            }
        }
    }

    /// <summary>Poked, or buzzed by the cat: leap in fright, then run away from where it came from.</summary>
    private void Spook(float fromX)
    {
        _scareX = fromX;
        _startleCooldown = StartleCooldown;
        Launch(SheepMode.Startle, Position, _platform!.Value, Size.Height * 0.6f);
    }

    private RectangleF BodyRect => new(Position.X - Size.Width / 2f, Position.Y - Size.Height, Size.Width, Size.Height);

    /// <summary>
    /// Follows the platform the sheep stands on if it has moved (a dragged window carries its
    /// sheep along), and starts a fall if it is no longer underfoot.
    /// </summary>
    private void KeepFooting()
    {
        var current = _platform!.Value;
        foreach (var candidate in _platforms)
        {
            if (candidate.Id != current.Id) continue;
            float dx = candidate.Anchor - current.Anchor;
            if (!candidate.Spans(Position.X + dx)) continue;

            Position = new PointF(Position.X + dx, candidate.Y);
            _targetX += dx;
            _platform = candidate;
            return;
        }

        StartFall();
    }

    private void StartFall()
    {
        Enter(SheepMode.Fall);
        _platform = null;
        _fallSpeed = 0;
    }

    private void UpdateFall(float dt)
    {
        float from = Position.Y;
        _fallSpeed = MathF.Min(_fallSpeed + Gravity * dt, Size.Width * 40f);
        float to = from + _fallSpeed * dt;

        Platform? landing = null;
        foreach (var platform in _platforms)
        {
            if (platform.Spans(Position.X) && platform.Y >= from && platform.Y <= to
                && (landing is null || platform.Y < landing.Value.Y))
            {
                landing = platform;
            }
        }

        if (landing is { } found)
        {
            Position = new PointF(Position.X, found.Y);
            _platform = found;
            Enter(SheepMode.Land);
        }
        else
        {
            Position = new PointF(Position.X, to);
            // Fell out of the world (its screen went away, say): come back in from the top.
            if (to > Bounds.Bottom + Size.Height * 2) DropIn();
        }
    }

    /// <summary>Drops the sheep in from above the screen, to land on whatever is below.</summary>
    private void DropIn()
    {
        var grounds = _platforms.Where(p => p.IsGround).ToArray();
        var ground = grounds[_rng.Next(grounds.Length)];
        var (min, max) = Range(ground);
        Position = new PointF(min + (float)_rng.NextDouble() * (max - min), Bounds.Top - Size.Height);
        StartFall();
    }

    private void ChooseNext()
    {
        var here = _platform!.Value;
        double roll = _rng.NextDouble();
        if (here.IsGround)
        {
            // With scenery about, some of the time it would have grazed or strolled goes on errands.
            if (roll >= 0.20 && roll < 0.42 && StartErrand(here)) return;
            if (roll < 0.30) StartTimed(SheepMode.Graze, 5f, 12f);
            else if (roll < 0.52) StartWalk();
            else if (roll < 0.66) StartJump();
            else if (roll < 0.73) StartTimed(SheepMode.Sit, 5f, 12f);
            else if (roll < 0.80) StartBaa();
            else if (roll < 0.87) StartPronk();
            else if (roll < 0.92) StartZoomies();
            else StartTimed(SheepMode.Idle, 1f, 3f);
        }
        else
        {
            // Up on a window there is no grass, so it mostly sits and enjoys the view.
            if (roll < 0.30) StartTimed(SheepMode.Sit, 5f, 12f);
            else if (roll < 0.50) StartWalk();
            else if (roll < 0.65) StartJump();
            else if (roll < 0.75) StartBaa();
            else if (roll < 0.82) StartPronk();
            else if (roll < 0.87) StartZoomies();
            else StartTimed(SheepMode.Idle, 1f, 3f);
        }
    }

    /// <summary>Sets off for one of the attractions on this ground. False if there are none.</summary>
    private bool StartErrand(Platform here)
    {
        var reachable = Attractions
            .Where(a => MathF.Abs(a.Spot.Y - here.Y) <= 2f && here.Spans(a.Spot.X))
            .ToArray();
        if (reachable.Length == 0) return false;

        var choice = reachable[_rng.Next(reachable.Length)];
        // Shade is for lying in; anything else it stands beside, on the side it comes from.
        float beside = choice.Errand == SheepErrand.Rest ? 0f : Size.Width * 0.6f;
        float side = Position.X <= choice.Spot.X ? -1 : 1;
        var (min, max) = Range(here);
        Enter(SheepMode.Walk);
        _targetX = Math.Clamp(choice.Spot.X + side * beside, min, max);
        _goingTo = choice;
        return true;
    }

    /// <summary>Having walked up to an attraction, does what it came for.</summary>
    private void Arrive(Attraction attraction)
    {
        float dx = attraction.Spot.X - Position.X;
        switch (attraction.Errand)
        {
            case SheepErrand.Drink: StartTimed(SheepMode.Graze, 4f, 8f); break;
            case SheepErrand.Munch: StartTimed(SheepMode.Graze, 6f, 12f); break;
            case SheepErrand.Rest: StartTimed(SheepMode.Sit, 6f, 12f); break;
            case SheepErrand.Warm: StartTimed(SheepMode.Sit, 8f, 14f); break;
            case SheepErrand.Greet: StartBaa(); break;
            default: ChooseNext(); return;
        }

        if (MathF.Abs(dx) > 1f) FacingRight = dx > 0;
        Errand = attraction.Errand;
    }

    private void Enter(SheepMode mode)
    {
        Mode = mode;
        ModeTime = 0;
        _jump = null;
        _goingTo = null;
        Errand = SheepErrand.None;
    }

    private void StartTimed(SheepMode mode, float minSeconds, float maxSeconds)
    {
        Enter(mode);
        _duration = minSeconds + (float)_rng.NextDouble() * (maxSeconds - minSeconds);
    }

    private void StartBaa()
    {
        Enter(SheepMode.Baa);
        _duration = BaaDuration;
        BaaCount++;
        Smitten = false;
    }

    private void StartWalk()
    {
        Enter(SheepMode.Walk);
        var (min, max) = Range(_platform!.Value);
        _targetX = min + (float)_rng.NextDouble() * (max - min);
    }

    /// <summary>
    /// Picks another platform within reach, walks to the best spot to take off from and leaps.
    /// Falls back to a stroll if there is nowhere to go.
    /// </summary>
    private void StartJump()
    {
        var here = _platform!.Value;
        var (hereMin, hereMax) = Range(here);
        float reach = Size.Width * 6f;

        var options = new List<(Platform Platform, float TakeOff, float Landing)>();
        foreach (var platform in _platforms)
        {
            if (platform.Id == here.Id || platform.Width < Size.Width) continue;
            var (min, max) = Range(platform);
            // Land as close to straight above or below as the two ledges allow.
            float takeOff = Math.Clamp(Math.Clamp(Position.X, min, max), hereMin, hereMax);
            float landing = Math.Clamp(takeOff, min, max);
            if (MathF.Abs(landing - takeOff) <= reach) options.Add((platform, takeOff, landing));
        }

        if (options.Count == 0)
        {
            StartWalk();
            return;
        }

        var choice = options[_rng.Next(options.Count)];
        // Don't always hug the nearest end: shuffle a little further in when there is room.
        var (landMin, landMax) = Range(choice.Platform);
        float nudge = ((float)_rng.NextDouble() - 0.5f) * Size.Width * 2f;
        float landingX = Math.Clamp(choice.Landing + nudge, landMin, landMax);
        if (MathF.Abs(landingX - choice.TakeOff) > reach) landingX = choice.Landing;

        Enter(SheepMode.Walk);
        _targetX = choice.TakeOff;
        _jump = (choice.Platform.Id, landingX - choice.Platform.Anchor);
    }

    /// <summary>Leaps to the platform chosen in <see cref="StartJump"/>, if it is still there.</summary>
    private void TakeOff(long id, float offsetFromAnchor)
    {
        foreach (var platform in _platforms)
        {
            if (platform.Id != id) continue;
            var (min, max) = Range(platform);
            float x = Math.Clamp(platform.Anchor + offsetFromAnchor, min, max);
            if (!platform.Spans(x)) continue;
            Launch(SheepMode.Jump, new PointF(x, platform.Y), platform, Size.Height * 0.8f);
            return;
        }

        StartTimed(SheepMode.Idle, 1f, 2f);
    }

    private void StartPronk()
    {
        _direction = FacingRight ? 1 : -1;
        _repeats = _rng.Next(3, 6);
        Hop();
    }

    /// <summary>One bouncy hop along the platform, turning round at the end of it.</summary>
    private void Hop()
    {
        var here = _platform!.Value;
        var (min, max) = Range(here);
        float stride = Size.Width * 0.9f;
        float x = Position.X + _direction * stride;
        if (x < min || x > max)
        {
            _direction = -_direction;
            x = Math.Clamp(Position.X + _direction * stride, min, max);
        }

        Launch(SheepMode.Pronk, new PointF(x, here.Y), here, Size.Height * 0.45f);
    }

    private void StartZoomies()
    {
        Enter(SheepMode.Zoomies);
        _direction = FacingRight ? 1 : -1;
        _repeats = _rng.Next(2, 5);
        AimZoomies();
    }

    private void AimZoomies()
    {
        var (min, max) = Range(_platform!.Value);
        float dash = Size.Width * (3f + (float)_rng.NextDouble() * 4f);
        float x = Math.Clamp(Position.X + _direction * dash, min, max);
        // Not enough room that way: go the other way first.
        if (MathF.Abs(x - Position.X) < Size.Width) x = Math.Clamp(Position.X - _direction * dash, min, max);
        _targetX = x;
        _direction = x >= Position.X ? -1 : 1;
    }

    private void StartFlee()
    {
        Enter(SheepMode.Flee);
        var (min, max) = Range(_platform!.Value);
        float run = Size.Width * 5f;
        float away = _scareX > Position.X ? -1 : 1;
        float x = Math.Clamp(Position.X + away * run, min, max);
        // Cornered: the only way out is past whatever scared it.
        if (MathF.Abs(x - Position.X) < Size.Width * 1.5f) x = Math.Clamp(Position.X - away * run, min, max);
        _targetX = x;
    }

    /// <summary>Starts a ballistic leap to <paramref name="to"/>, clearing the higher end by <paramref name="clearance"/>.</summary>
    private void Launch(SheepMode mode, PointF to, Platform landing, float clearance)
    {
        Enter(mode);
        _platform = null;
        _landing = landing;
        _arcFrom = Position;
        _arcTo = to;

        float apex = MathF.Min(Position.Y, to.Y) - clearance;
        float up = Position.Y - apex, down = to.Y - apex;
        _arcVy = -MathF.Sqrt(2 * Gravity * up);
        _arcDuration = MathF.Sqrt(2 * up / Gravity) + MathF.Sqrt(2 * down / Gravity);
        _arcVx = (to.X - Position.X) / _arcDuration;
        if (MathF.Abs(to.X - Position.X) > 1f) FacingRight = to.X > Position.X;
    }

    /// <summary>
    /// Advances the current leap. Returns true on touching down; if the platform aimed for has
    /// gone or moved in the meantime the sheep misses, and falls instead.
    /// </summary>
    private bool FlyArc()
    {
        if (ModeTime < _arcDuration)
        {
            float t = ModeTime;
            Position = new PointF(_arcFrom.X + _arcVx * t, _arcFrom.Y + _arcVy * t + 0.5f * Gravity * t * t);
            return false;
        }

        Position = _arcTo;
        foreach (var platform in _platforms)
        {
            if (platform.Id == _landing.Id && platform.Y == _landing.Y && platform.Spans(Position.X))
            {
                _platform = platform;
                return true;
            }
        }

        StartFall();
        return false;
    }

    private bool WalkToward(float x, float speed, float dt)
    {
        float dx = x - Position.X;
        float step = speed * dt;
        if (MathF.Abs(dx) > 1f) FacingRight = dx > 0;
        bool arrived = MathF.Abs(dx) <= step;
        Position = new PointF(arrived ? x : Position.X + MathF.Sign(dx) * step, Position.Y);
        return arrived;
    }

    /// <summary>The stretch of a platform the sheep can stand on without hanging over an end.</summary>
    private (float Min, float Max) Range(Platform platform)
    {
        float half = Size.Width / 2f;
        float min = platform.Left + half, max = platform.Right - half;
        if (max > min) return (min, max);
        float middle = (platform.Left + platform.Right) / 2f;
        return (middle, middle);
    }
}
