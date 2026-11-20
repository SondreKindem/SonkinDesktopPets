namespace SonkinDesktopPets;

public enum NyanMode
{
    Roam,
    Hover,
    Dash,
    ChaseCursor,
    CarryCursor,
    FetchNote,
    DeliverNote,
    Flee,
    Loop,
    Wave,
    OrbitCursor,
    Sprinkle,
    Sing,
    CollectNote,
    RemoveNote,
    VisitSheep,
    CarrySheep,
    Perch,
}

public readonly record struct TrailSegment(PointF Position, float Born, int Index);

public readonly record struct Star(PointF Position, float Born, int Tint);

public readonly record struct Crumb(PointF Position, PointF Velocity, float Born, int Tint);

/// <summary>
/// The cat's behaviour, in screen coordinates. Knows nothing about windows or
/// the real mouse: the host feeds it the cursor position and applies
/// <see cref="CursorGrab"/> and <see cref="NoteDropped"/> to the real desktop.
/// </summary>
public sealed class NyanBrain
{
    public const float TrailLife = 1.2f;
    public const float StarLife = 0.55f;
    public const float CrumbLife = 1.2f;

    /// <summary>Minimum time between two note deliveries.</summary>
    public const float NoteCooldown = 120f;

    /// <summary>Minimum time between two visits to the sheep.</summary>
    public const float SheepCooldown = 30f;

    /// <summary>Minimum time between two sits on top of the scenery.</summary>
    public const float PerchCooldown = 25f;

    private const float FirstSheepDelay = 10f;
    private const float VisitTimeout = 8f;
    private const float CarrySheepDuration = 4f;
    private const float FirstNoteDelay = 45f;
    private const float LoopSpeed = 420f;
    private const float WaveSpeed = 340f;
    private const float OrbitDuration = 4f;
    private const float OrbitTurnRate = 3.5f;
    private const float SprinkleDuration = 1.5f;
    private const float SingDuration = 2f;
    private const float CrumbGravity = 700f;

    private const float RoamSpeed = 260f;
    private const float DashSpeed = 900f;
    private const float ChaseSpeed = 520f;
    private const float CarrySpeed = 420f;
    private const float FetchSpeed = 520f;
    private const float DeliverSpeed = 300f;
    private const float FleeSpeed = 800f;

    private const float CarryDuration = 2.5f;
    private const float ChaseTimeout = 6f;
    private const float MischiefCooldown = 20f;
    private const float FleeCooldown = 3f;

    private readonly Random _rng;
    private readonly List<TrailSegment> _trail = new();
    private readonly List<Star> _stars = new();

    private Rectangle[] _regions = Array.Empty<Rectangle>();
    private PointF _target;
    private float _modeTime;
    private float _hoverFor;
    private float _trailDistance;
    private int _trailIndex;
    private float _starTimer;
    private float _mischiefCooldown;
    private float _fleeCooldown;
    private int _dashPhase;
    private bool _dashRight;
    private readonly List<Crumb> _crumbs = new();
    private float _noteCooldown = FirstNoteDelay;
    private float _crumbTimer;
    private PointF _circleCentre;
    private float _circleRadius;
    private float _circleAngle;
    private float _circleTurn;
    private float _circleRemaining;
    private PointF _waveStart;
    private float _waveBaseY;
    private float _waveEndX;
    private float _sheepCooldown = FirstSheepDelay;
    private PointF _sheepSpot;
    private bool _abducting;
    private float _perchCooldown;
    private bool _settled;

    public NyanBrain(Random rng, Size catSize, Rectangle bounds, IReadOnlyList<Rectangle>? regions = null)
    {
        _rng = rng;
        CatSize = catSize;
        SetArea(bounds, regions);
    }

    /// <summary>Whether the cat may grab the mouse cursor and fly off with it.</summary>
    public bool MischiefEnabled { get; set; } = true;

    /// <summary>Whether the cat may fetch notes from off screen.</summary>
    public bool NotesEnabled { get; set; } = true;

    /// <summary>Set by the host: false while the desktop already holds enough notes.</summary>
    public bool NoteSlotAvailable { get; set; } = true;

    public Size CatSize { get; set; }

    /// <summary>The area the cat lives in.</summary>
    public Rectangle Bounds { get; private set; }

    /// <summary>Centre of the cat.</summary>
    public PointF Position { get; private set; }

    public bool FacingRight { get; private set; } = true;

    public NyanMode Mode { get; private set; }

    public float Time { get; private set; }

    public bool CarryingNote => Mode is NyanMode.DeliverNote or NyanMode.RemoveNote;

    /// <summary>
    /// Set by the host to the top-centre of a note that has outstayed its welcome. The cat
    /// goes to fetch it once it finishes what it is doing; clear it if the note goes away.
    /// </summary>
    public Point? NoteToCollect { get; set; }

    /// <summary>Raised when the cat has reached <see cref="NoteToCollect"/> and takes the note away.</summary>
    public event Action? NoteCollected;

    /// <summary>Where the host must put the mouse cursor this frame, if the cat is holding it.</summary>
    public Point? CursorGrab { get; private set; }

    public IReadOnlyList<TrailSegment> Trail => _trail;

    public IReadOnlyList<Star> Stars => _stars;

    /// <summary>Pastry crumbs shaken loose, falling under gravity.</summary>
    public IReadOnlyList<Crumb> Crumbs => _crumbs;

    /// <summary>How many times the cat has sung; the host uses it to vary the lyric.</summary>
    public int SingCount { get; private set; }

    /// <summary>Raised with the point just below the cat where it let go of a note.</summary>
    public event Action<Point>? NoteDropped;

    /// <summary>
    /// Set by the host every frame: the top-centre of each sheep that is standing around
    /// where the cat could get at it. Now and then the cat pays one of them a visit.
    /// </summary>
    public IReadOnlyList<PointF> Sheep { get; set; } = Array.Empty<PointF>();

    /// <summary>
    /// Where the host must hang the sheep the cat has snatched this frame (by the top of its
    /// fleece), or null if the cat is not carrying one. The sheep is let go when this clears.
    /// </summary>
    public PointF? SheepHold { get; private set; }

    /// <summary>Raised with a sheep's spot when the cat stops above it to sing to it.</summary>
    public event Action<PointF>? SheepSerenaded;

    /// <summary>
    /// Set by the host: the tops of tall things in the scenery (a lighthouse, a summit). Now
    /// and then the cat flies over to sit on one.
    /// </summary>
    public IReadOnlyList<PointF> Perches { get; set; } = Array.Empty<PointF>();

    /// <summary>True while the cat is sitting on a perch, as opposed to flying to it.</summary>
    public bool Perched => Mode == NyanMode.Perch && _settled;

    /// <summary>True while the cat is tearing about fast enough to frighten a sheep it passes.</summary>
    public bool Swooping => Mode is NyanMode.Dash or NyanMode.Wave or NyanMode.Loop or NyanMode.Flee;

    /// <summary>
    /// Confines the cat to <paramref name="bounds"/>. <paramref name="regions"/> are the
    /// visible parts of it (the monitors); destinations are only picked inside those.
    /// </summary>
    public void SetArea(Rectangle bounds, IReadOnlyList<Rectangle>? regions = null)
    {
        Bounds = bounds;
        _regions = regions is { Count: > 0 } ? regions.ToArray() : new[] { bounds };
        _trail.Clear();
        _stars.Clear();
        _crumbs.Clear();
        CursorGrab = null;
        SheepHold = null;
        Position = ClampInside(Position);
        StartRoam();
    }

    public void Update(float dt, Point cursor)
    {
        Time += dt;
        _modeTime += dt;
        _mischiefCooldown = MathF.Max(0, _mischiefCooldown - dt);
        _fleeCooldown = MathF.Max(0, _fleeCooldown - dt);
        _noteCooldown = MathF.Max(0, _noteCooldown - dt);
        _sheepCooldown = MathF.Max(0, _sheepCooldown - dt);
        _perchCooldown = MathF.Max(0, _perchCooldown - dt);
        CursorGrab = null;
        SheepHold = null;

        switch (Mode)
        {
            case NyanMode.Roam:
                if (MoveToward(_target, RoamSpeed, dt)) ChooseNext(cursor);
                break;

            case NyanMode.Hover:
            case NyanMode.Sing:
                if (_modeTime >= _hoverFor) ChooseNext(cursor);
                break;

            case NyanMode.Sprinkle:
                ShakeCrumbs(dt);
                if (_modeTime >= SprinkleDuration) ChooseNext(cursor);
                break;

            case NyanMode.Loop:
                float turned = LoopSpeed / _circleRadius * dt;
                _circleAngle += _circleTurn * turned;
                _circleRemaining -= turned;
                MoveTo(PointOnCircle(_circleCentre));
                if (_circleRemaining <= 0) StartRoam();
                break;

            case NyanMode.Wave:
                UpdateWave(dt);
                break;

            case NyanMode.OrbitCursor:
                if (!Bounds.Contains(cursor) || _modeTime > OrbitDuration)
                {
                    StartRoam();
                }
                else
                {
                    // Chase a point that circles the cursor; the cat settles into the orbit.
                    _circleAngle += _circleTurn * OrbitTurnRate * dt;
                    MoveToward(PointOnCircle(cursor), ChaseSpeed, dt);
                }
                break;

            case NyanMode.Dash:
                UpdateDash(dt);
                break;

            case NyanMode.ChaseCursor:
                if (!Bounds.Contains(cursor) || _modeTime > ChaseTimeout)
                {
                    StartRoam();
                }
                else if (MoveToward(cursor, ChaseSpeed, dt) || Distance(Position, cursor) < CatSize.Width * 0.3f)
                {
                    Enter(NyanMode.CarryCursor);
                    _target = RandomPoint();
                }
                break;

            case NyanMode.CarryCursor:
                bool arrived = MoveToward(_target, CarrySpeed, dt);
                CursorGrab = ClampCursor(MouthPosition);
                if (arrived || _modeTime > CarryDuration)
                {
                    _mischiefCooldown = MischiefCooldown;
                    StartHover(1f);
                }
                break;

            case NyanMode.FetchNote:
                if (MoveToward(_target, FetchSpeed, dt))
                {
                    Enter(NyanMode.DeliverNote);
                    _target = RandomPoint();
                }
                break;

            case NyanMode.DeliverNote:
                if (MoveToward(_target, DeliverSpeed, dt))
                {
                    NoteDropped?.Invoke(new Point((int)Position.X, (int)(Position.Y + CatSize.Height / 2f)));
                    _noteCooldown = NoteCooldown;
                    StartHover(1f);
                }
                break;

            case NyanMode.Flee:
                if (MoveToward(_target, FleeSpeed, dt)) StartRoam();
                break;

            case NyanMode.CollectNote:
                if (NoteToCollect is not { } note || !Bounds.Contains(note))
                {
                    // The note was closed or dragged out of reach on the way there.
                    StartRoam();
                }
                else if (MoveToward(new PointF(note.X, note.Y - CatSize.Height * 0.6f), FetchSpeed, dt))
                {
                    NoteToCollect = null;
                    NoteCollected?.Invoke();
                    Enter(NyanMode.RemoveNote);
                    _target = NearestExit();
                }
                break;

            case NyanMode.RemoveNote:
                if (MoveToward(_target, FetchSpeed, dt)) StartRoam();
                break;

            case NyanMode.VisitSheep:
                if (!TrackSheep() || _modeTime > VisitTimeout)
                {
                    // It wandered off, jumped away or was put away before the cat got there.
                    StartRoam();
                    break;
                }

                // Close enough to grab it by the fleece, or a polite distance above to sing.
                float height = CatSize.Height * (_abducting ? 0.45f : 1.1f);
                if (!MoveToward(new PointF(_sheepSpot.X, _sheepSpot.Y - height), ChaseSpeed, dt)) break;

                _sheepCooldown = SheepCooldown;
                if (_abducting)
                {
                    Enter(NyanMode.CarrySheep);
                    _target = RandomPoint();
                    SheepHold = Grip;
                }
                else
                {
                    StartSing();
                    SheepSerenaded?.Invoke(_sheepSpot);
                }

                break;

            case NyanMode.CarrySheep:
                // Reaching the spot, or tiring of it, lets the sheep go: it falls from there.
                if (MoveToward(_target, CarrySpeed, dt) || _modeTime > CarrySheepDuration) StartHover(1f);
                else SheepHold = Grip;
                break;

            case NyanMode.Perch:
                if (!_settled)
                {
                    if (!MoveToward(_target, RoamSpeed, dt)) break;
                    _settled = true;
                    _modeTime = 0;
                }
                else if (_modeTime >= _hoverFor)
                {
                    _perchCooldown = PerchCooldown;
                    // A good view sometimes calls for a song.
                    if (_rng.NextDouble() < 0.4) StartSing();
                    else ChooseNext(cursor);
                }

                break;
        }

        if ((Mode is NyanMode.Roam or NyanMode.Hover || Perched) && _fleeCooldown <= 0 && CatRect.Contains(cursor))
        {
            StartFlee(cursor);
        }

        UpdateStars(dt);
        UpdateCrumbs(dt);
        _trail.RemoveAll(s => Time - s.Born > TrailLife);
    }

    private PointF PointOnCircle(PointF centre) => new(
        centre.X + _circleRadius * MathF.Cos(_circleAngle),
        centre.Y + _circleRadius * MathF.Sin(_circleAngle));

    /// <summary>One or two loop-the-loops, upwards if there is room above, otherwise downwards.</summary>
    private void StartLoop()
    {
        Enter(NyanMode.Loop);
        _circleRadius = CatSize.Width * 1.2f;
        bool upward = Position.Y - 2 * _circleRadius - CatSize.Height / 2f >= Bounds.Top;
        float forward = FacingRight ? 1 : -1;
        // Screen y points down, so the cat starts at the bottom (or top) of the circle
        // and turns whichever way carries it forward first.
        _circleCentre = new PointF(Position.X, Position.Y + (upward ? -_circleRadius : _circleRadius));
        _circleAngle = upward ? MathF.PI / 2f : -MathF.PI / 2f;
        _circleTurn = upward ? -forward : forward;
        _circleRemaining = MathF.PI * 2f * _rng.Next(1, 3);
    }

    /// <summary>Crosses the screen in a big sine wave, drawing a wavy rainbow.</summary>
    private void StartWave()
    {
        Enter(NyanMode.Wave);
        float amplitude = CatSize.Height * 1.2f;
        bool goRight = Position.X < Bounds.Left + Bounds.Width / 2f;
        _waveStart = Position;
        _waveBaseY = ClampAxis(Position.Y, Bounds.Top, Bounds.Bottom, amplitude + CatSize.Height / 2f);
        float travel = Bounds.Width * (0.4f + (float)_rng.NextDouble() * 0.4f);
        _waveEndX = ClampInside(new PointF(Position.X + (goRight ? travel : -travel), Position.Y)).X;
    }

    private void UpdateWave(float dt)
    {
        float direction = _waveEndX >= _waveStart.X ? 1 : -1;
        float x = Position.X + direction * WaveSpeed * dt;
        float travelled = MathF.Abs(x - _waveStart.X);
        // Ease from the starting height onto the wave's centre line instead of jumping to it.
        float ease = MathF.Max(0, 1 - travelled / 200f);
        float y = _waveBaseY
            + (_waveStart.Y - _waveBaseY) * ease
            + CatSize.Height * 1.2f * MathF.Sin(travelled / (CatSize.Width * 0.9f));
        MoveTo(new PointF(x, y));
        if ((x - _waveEndX) * direction >= 0) StartRoam();
    }

    private void StartOrbit()
    {
        Enter(NyanMode.OrbitCursor);
        _circleRadius = CatSize.Width * 1.1f;
        _circleAngle = (float)(_rng.NextDouble() * Math.PI * 2);
        _circleTurn = _rng.Next(2) == 0 ? 1 : -1;
    }

    /// <summary>Stops to sing, with a ring of stars popping around it.</summary>
    private void StartSing()
    {
        Enter(NyanMode.Sing);
        _hoverFor = SingDuration;
        SingCount++;
        const int ring = 10;
        for (int i = 0; i < ring; i++)
        {
            float angle = i * MathF.PI * 2f / ring;
            var position = new PointF(
                Position.X + CatSize.Width * 0.9f * MathF.Cos(angle),
                Position.Y + CatSize.Height * 1.1f * MathF.Sin(angle));
            // Born slightly in the future so the ring twinkles round rather than all at once.
            _stars.Add(new Star(position, Time + i * 0.04f, _rng.Next(4)));
        }
    }

    private void ShakeCrumbs(float dt)
    {
        _crumbTimer -= dt;
        if (_crumbTimer > 0) return;
        _crumbTimer = 0.03f;

        var position = new PointF(
            Position.X + ((float)_rng.NextDouble() - 0.5f) * CatSize.Width * 0.6f,
            Position.Y + CatSize.Height * 0.4f);
        var velocity = new PointF(
            ((float)_rng.NextDouble() - 0.5f) * 240f,
            -60f - (float)_rng.NextDouble() * 200f);
        _crumbs.Add(new Crumb(position, velocity, Time, _rng.Next(3)));
    }

    private void UpdateCrumbs(float dt)
    {
        _crumbs.RemoveAll(c => Time - c.Born > CrumbLife);
        for (int i = 0; i < _crumbs.Count; i++)
        {
            var crumb = _crumbs[i];
            var velocity = new PointF(crumb.Velocity.X, crumb.Velocity.Y + CrumbGravity * dt);
            _crumbs[i] = crumb with
            {
                Velocity = velocity,
                Position = new PointF(crumb.Position.X + velocity.X * dt, crumb.Position.Y + velocity.Y * dt),
            };
        }
    }

    /// <summary>Moves straight to a point on a scripted path, keeping facing and trail in step.</summary>
    private void MoveTo(PointF point)
    {
        float dx = point.X - Position.X;
        var from = Position;
        if (MathF.Abs(dx) > 0.5f) FacingRight = dx > 0;
        Position = point;
        LayTrail(from, point);
    }

    private RectangleF CatRect => new(
        Position.X - CatSize.Width / 2f,
        Position.Y - CatSize.Height / 2f,
        CatSize.Width,
        CatSize.Height);

    private PointF MouthPosition => new(
        Position.X + (FacingRight ? 1 : -1) * CatSize.Width * 0.45f,
        Position.Y + CatSize.Height * 0.15f);

    /// <summary>Where a carried sheep hangs from: just inside the bottom of the cat.</summary>
    private PointF Grip => new(Position.X, Position.Y + CatSize.Height * 0.45f);

    private void StartSheepVisit()
    {
        var within = Sheep.Where(spot => Bounds.Contains(Point.Round(spot))).ToArray();
        if (within.Length == 0)
        {
            StartRoam();
            return;
        }

        Enter(NyanMode.VisitSheep);
        _sheepSpot = within[_rng.Next(within.Length)];
        _abducting = _rng.Next(2) == 0;
    }

    /// <summary>Flies to one of the perches in its area and sits on it for a while.</summary>
    private void StartPerch()
    {
        var within = Perches.Where(spot => Bounds.Contains(Point.Round(spot))).ToArray();
        if (within.Length == 0)
        {
            StartRoam();
            return;
        }

        var perch = within[_rng.Next(within.Length)];
        Enter(NyanMode.Perch);
        _settled = false;
        _hoverFor = 4f + (float)_rng.NextDouble() * 5f;
        // Sitting on it: the cat's feet just below the top of the thing.
        _target = ClampInside(new PointF(perch.X, perch.Y - CatSize.Height * 0.4f));
    }

    /// <summary>
    /// Keeps following the sheep the cat set off for as it moves. False once there is no
    /// sheep anywhere near where it last was.
    /// </summary>
    private bool TrackSheep()
    {
        float nearest = CatSize.Width * 2f;
        bool found = false;
        var last = _sheepSpot;
        foreach (var spot in Sheep)
        {
            float distance = Distance(spot, last);
            if (distance > nearest) continue;
            (nearest, found, _sheepSpot) = (distance, true, spot);
        }

        return found;
    }

    /// <summary>A point just off the nearer side edge, at the cat's current height.</summary>
    private PointF NearestExit()
    {
        bool leftIsNearer = Position.X < Bounds.Left + Bounds.Width / 2f;
        return new PointF(leftIsNearer ? Bounds.Left - CatSize.Width : Bounds.Right + CatSize.Width, Position.Y);
    }

    private void Enter(NyanMode mode)
    {
        Mode = mode;
        _modeTime = 0;
    }

    private void StartRoam()
    {
        Enter(NyanMode.Roam);
        _target = RandomPoint();
    }

    private void StartHover(float seconds)
    {
        Enter(NyanMode.Hover);
        _hoverFor = seconds;
    }

    private void StartFlee(Point cursor)
    {
        float dx = Position.X - cursor.X, dy = Position.Y - cursor.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 1f)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            (dx, dy, length) = ((float)Math.Cos(angle), (float)Math.Sin(angle), 1f);
        }

        var away = ClampInside(new PointF(Position.X + dx / length * 450f, Position.Y + dy / length * 450f));
        // Cornered: running "away" would go nowhere, so bolt somewhere else instead.
        _target = Distance(away, Position) < 150f ? RandomPoint() : away;
        _fleeCooldown = FleeCooldown;
        Enter(NyanMode.Flee);
    }

    private void ChooseNext(Point cursor)
    {
        if (NoteToCollect is { } stale && Bounds.Contains(stale))
        {
            Enter(NyanMode.CollectNote);
            return;
        }

        double roll = _rng.NextDouble();
        if (roll < 0.08&& MischiefEnabled && _mischiefCooldown <= 0 && Bounds.Contains(cursor))
        {
            Enter(NyanMode.ChaseCursor);
        }
        else if (roll < 0.12 && NotesEnabled && NoteSlotAvailable && _noteCooldown <= 0)
        {
            Enter(NyanMode.FetchNote);
            _target = NearestExit();
        }
        else if (roll < 0.22)
        {
            Enter(NyanMode.Dash);
            _dashPhase = 0;
            _dashRight = FacingRight;
            _target = new PointF(_dashRight ? Bounds.Right + CatSize.Width : Bounds.Left - CatSize.Width, Position.Y);
        }
        else if (roll < 0.32)
        {
            StartLoop();
        }
        else if (roll < 0.42)
        {
            StartWave();
        }
        else if (roll < 0.49 && Bounds.Contains(cursor))
        {
            StartOrbit();
        }
        else if (roll < 0.56)
        {
            Enter(NyanMode.Sprinkle);
        }
        else if (roll < 0.63)
        {
            StartSing();
        }
        else if (roll < 0.75)
        {
            StartHover(0.8f + (float)_rng.NextDouble() * 1.7f);
        }
        else if (roll < 0.87 && Sheep.Count > 0 && _sheepCooldown <= 0)
        {
            StartSheepVisit();
        }
        else if (roll < 0.97 && Perches.Count > 0 && _perchCooldown <= 0)
        {
            StartPerch();
        }
        else
        {
            StartRoam();
        }
    }

    /// <summary>Shoots off one edge, wraps around and streaks back in from the opposite one.</summary>
    private void UpdateDash(float dt)
    {
        if (!MoveToward(_target, DashSpeed, dt)) return;

        if (_dashPhase == 0)
        {
            _dashPhase = 1;
            float y = RandomPoint().Y;
            float travel = Bounds.Width * (0.55f + (float)_rng.NextDouble() * 0.3f);
            Position = new PointF(_dashRight ? Bounds.Left - CatSize.Width : Bounds.Right + CatSize.Width, y);
            _target = ClampInside(new PointF(_dashRight ? Bounds.Left + travel : Bounds.Right - travel, y));
        }
        else
        {
            StartRoam();
        }
    }

    private void UpdateStars(float dt)
    {
        _stars.RemoveAll(s => Time - s.Born > StarLife);
        if (Mode is NyanMode.Hover or NyanMode.Sing or NyanMode.Sprinkle || Perched) return;

        _starTimer -= dt;
        if (_starTimer > 0) return;
        _starTimer = Mode == NyanMode.Dash ? 0.04f : 0.15f;

        float behind = (FacingRight ? -1 : 1) * (float)_rng.NextDouble() * CatSize.Width * 1.5f;
        float spread = ((float)_rng.NextDouble() * 2f - 1f) * CatSize.Height * 1.2f;
        _stars.Add(new Star(new PointF(Position.X + behind, Position.Y + spread), Time, _rng.Next(4)));
    }

    private bool MoveToward(PointF target, float speed, float dt)
    {
        float dx = target.X - Position.X, dy = target.Y - Position.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float step = speed * dt;
        if (MathF.Abs(dx) > 2f) FacingRight = dx > 0;

        var from = Position;
        bool arrived = distance <= step;
        Position = arrived
            ? target
            : new PointF(Position.X + dx / distance * step, Position.Y + dy / distance * step);
        LayTrail(from, Position);
        return arrived;
    }

    /// <summary>
    /// Drops a rainbow segment every <c>spacing</c> pixels along the path just flown. Segments
    /// are placed where the cat was when it crossed each mark, not where it ended the frame, so
    /// a fast cat or a slow frame still leaves an unbroken rainbow.
    /// </summary>
    private void LayTrail(PointF from, PointF to)
    {
        float spacing = CatSize.Width / 4f;
        float length = Distance(from, to);
        float covered = 0;
        while (_trailDistance + (length - covered) >= spacing)
        {
            covered += spacing - _trailDistance;
            _trailDistance = 0;
            float t = covered / length;
            var at = new PointF(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
            // Exactly on the flight path, with no "behind the cat" offset: the sprite covers the
            // newest segment anyway, and an offset that flips with the facing tears loops in two.
            _trail.Add(new TrailSegment(at, Time, _trailIndex++));
        }

        _trailDistance += length - covered;
    }

    private PointF RandomPoint()
    {
        var region = _regions[_rng.Next(_regions.Length)];
        return new PointF(
            RandomWithin(region.Left, region.Right, CatSize.Width),
            RandomWithin(region.Top, region.Bottom, CatSize.Height));
    }

    private float RandomWithin(int low, int high, int margin)
    {
        float min = low + margin, max = high - margin;
        return max <= min ? (low + high) / 2f : min + (float)_rng.NextDouble() * (max - min);
    }

    private PointF ClampInside(PointF point) => new(
        ClampAxis(point.X, Bounds.Left, Bounds.Right, CatSize.Width / 2f),
        ClampAxis(point.Y, Bounds.Top, Bounds.Bottom, CatSize.Height / 2f));

    private static float ClampAxis(float value, int low, int high, float margin)
    {
        float min = low + margin, max = high - margin;
        return max <= min ? (low + high) / 2f : Math.Clamp(value, min, max);
    }

    private Point ClampCursor(PointF point) => new(
        Math.Clamp((int)point.X, Bounds.Left, Math.Max(Bounds.Left, Bounds.Right - 1)),
        Math.Clamp((int)point.Y, Bounds.Top, Math.Max(Bounds.Top, Bounds.Bottom - 1)));

    private static float Distance(PointF a, PointF b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
