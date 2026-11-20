using System.Drawing;
using Xunit;

namespace SonkinDesktopPets.Tests;

public class NyanBrainTests
{
    // A secondary monitor to the right of the primary one, so (0,0)-based mistakes show up.
    private static readonly Rectangle Monitor = new(1920, 0, 1920, 1080);
    private static readonly Point MonitorCentre = new(2880, 540);
    private static readonly Point OnOtherMonitor = new(500, 500);
    private static readonly Size Cat = new(136, 88);
    private const float Frame = 1f / 60f;

    private static NyanBrain CreateBrain(int seed = 1) => new(new Random(seed), Cat, Monitor);

    private static void Simulate(NyanBrain brain, float seconds, Point cursor, Action? afterEachFrame = null)
    {
        for (int i = 0; i < seconds / Frame; i++)
        {
            brain.Update(Frame, cursor);
            afterEachFrame?.Invoke();
        }
    }

    [Fact]
    public void Stolen_cursor_is_never_dragged_off_the_cats_screen()
    {
        var brain = CreateBrain();
        var grabs = new List<Point>();

        Simulate(brain, 900, MonitorCentre, () =>
        {
            if (brain.CursorGrab is { } grab) grabs.Add(grab);
        });

        Assert.NotEmpty(grabs);
        Assert.All(grabs, grab => Assert.True(Monitor.Contains(grab), $"{grab} is outside {Monitor}"));
    }

    [Fact]
    public void Cursor_is_left_alone_when_mischief_is_off()
    {
        var brain = CreateBrain();
        brain.MischiefEnabled = false;

        Simulate(brain, 900, MonitorCentre, () => Assert.Null(brain.CursorGrab));
    }

    [Fact]
    public void Cursor_on_another_screen_is_never_chased_or_grabbed()
    {
        var brain = CreateBrain();

        Simulate(brain, 900, OnOtherMonitor, () =>
        {
            Assert.Null(brain.CursorGrab);
            Assert.NotEqual(NyanMode.ChaseCursor, brain.Mode);
            Assert.NotEqual(NyanMode.OrbitCursor, brain.Mode);
        });
    }

    [Fact]
    public void Notes_are_delivered_only_occasionally()
    {
        var brain = CreateBrain();
        var dropTimes = new List<float>();
        brain.NoteDropped += _ => dropTimes.Add(brain.Time);

        Simulate(brain, 3600, OnOtherMonitor);

        Assert.True(dropTimes.Count >= 2, $"only {dropTimes.Count} notes in an hour");
        var gaps = dropTimes.Zip(dropTimes.Skip(1), (earlier, later) => later - earlier);
        Assert.All(gaps, gap => Assert.True(gap >= NyanBrain.NoteCooldown, $"notes only {gap}s apart"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void No_action_carries_the_cat_far_from_its_screen(int seed)
    {
        var brain = CreateBrain(seed);
        // Dashes and note runs leave by one cat-length; loops and orbits may poke over an edge.
        var reachable = Rectangle.Inflate(Monitor, Cat.Width * 2, Cat.Width * 2);
        var modesSeen = new HashSet<NyanMode>();

        Simulate(brain, 1800, MonitorCentre, () =>
        {
            modesSeen.Add(brain.Mode);
            Assert.True(reachable.Contains(Point.Round(brain.Position)), $"{brain.Mode} took the cat to {brain.Position}");
        });

        // Note collection only happens on request from the host, which this test never makes,
        // and with no sheep about there are none to visit.
        var spontaneous = Enum.GetValues<NyanMode>().Except(new[]
        {
            NyanMode.CollectNote, NyanMode.RemoveNote, NyanMode.VisitSheep, NyanMode.CarrySheep,
        });
        Assert.Equal(spontaneous.ToHashSet(), modesSeen);
    }

    private static readonly PointF SheepSpot = new(2600, 980);

    private static NyanBrain CreateBrainWithSheep()
    {
        var brain = CreateBrain();
        brain.MischiefEnabled = false;
        brain.NotesEnabled = false;
        brain.Sheep = new[] { SheepSpot };
        return brain;
    }

    [Fact]
    public void A_sheep_is_snatched_from_where_it_stands_and_let_go_inside_the_screen()
    {
        var brain = CreateBrainWithSheep();
        var pickups = new List<PointF>();
        var drops = new List<PointF>();
        PointF? previous = null;

        Simulate(brain, 1800, OnOtherMonitor, () =>
        {
            var hold = brain.SheepHold;
            if (hold is { } taken && previous is null) pickups.Add(taken);
            if (hold is null && previous is { } last) drops.Add(last);
            previous = hold;
        });

        Assert.NotEmpty(pickups);
        Assert.All(pickups, grip =>
        {
            Assert.InRange(grip.X, SheepSpot.X - 1f, SheepSpot.X + 1f);
            Assert.InRange(grip.Y, SheepSpot.Y - 1f, SheepSpot.Y + 1f);
        });
        Assert.NotEmpty(drops);
        Assert.All(drops, grip => Assert.True(Monitor.Contains(Point.Round(grip)), $"sheep let go at {grip}, outside {Monitor}"));
    }

    [Fact]
    public void The_cat_sings_to_a_sheep_from_just_above_it()
    {
        var brain = CreateBrainWithSheep();
        int serenades = 0;
        brain.SheepSerenaded += spot =>
        {
            serenades++;
            Assert.Equal(SheepSpot, spot);
            Assert.Equal(NyanMode.Sing, brain.Mode);
            Assert.InRange(brain.Position.X, SheepSpot.X - 1f, SheepSpot.X + 1f);
            Assert.InRange(brain.Position.Y, SheepSpot.Y - Cat.Height * 2, SheepSpot.Y - Cat.Height / 2f);
        };

        Simulate(brain, 1800, OnOtherMonitor);

        Assert.True(serenades > 0, "the cat never sang to the sheep");
    }

    [Fact]
    public void The_cat_gives_the_sheep_some_peace_between_visits()
    {
        var brain = CreateBrainWithSheep();
        var visits = new List<float>();
        var previous = brain.Mode;

        Simulate(brain, 3600, OnOtherMonitor, () =>
        {
            if (brain.Mode == NyanMode.VisitSheep && previous != NyanMode.VisitSheep) visits.Add(brain.Time);
            previous = brain.Mode;
        });

        Assert.True(visits.Count >= 5, $"only {visits.Count} visits in an hour");
        var gaps = visits.Zip(visits.Skip(1), (earlier, later) => later - earlier);
        Assert.All(gaps, gap => Assert.True(gap >= NyanBrain.SheepCooldown, $"visits only {gap}s apart"));
    }

    [Fact]
    public void A_sheep_that_gets_away_before_the_cat_arrives_is_left_alone()
    {
        var brain = CreateBrainWithSheep();
        bool setOff = false, serenaded = false;
        brain.SheepSerenaded += _ => serenaded = true;

        Simulate(brain, 900, OnOtherMonitor, () =>
        {
            Assert.Null(brain.SheepHold);
            // The sheep leaps away the moment the cat sets off for it.
            bool coming = brain.Mode == NyanMode.VisitSheep;
            setOff |= coming;
            brain.Sheep = coming ? Array.Empty<PointF>() : new[] { SheepSpot };
        });

        Assert.True(setOff, "the cat never set off for the sheep");
        Assert.False(serenaded);
    }

    [Fact]
    public void An_expired_note_is_fetched_from_where_it_lies_and_carried_off_screen()
    {
        var brain = CreateBrain();
        brain.NotesEnabled = false;
        var note = new Point(2400, 700);
        PointF? catAtPickup = null;
        brain.NoteCollected += () => catAtPickup = brain.Position;
        bool carriedOffScreen = false;

        brain.NoteToCollect = note;
        Simulate(brain, 60, OnOtherMonitor, () =>
        {
            carriedOffScreen |= brain.CarryingNote && !Monitor.Contains(Point.Round(brain.Position));
        });

        Assert.NotNull(catAtPickup);
        Assert.InRange(catAtPickup.Value.X, note.X - 1f, note.X + 1f);
        Assert.InRange(catAtPickup.Value.Y, note.Y - Cat.Height, note.Y);
        Assert.True(carriedOffScreen, "the note never left the screen");
        Assert.Null(brain.NoteToCollect);
        Assert.False(brain.CarryingNote);
    }

    [Fact]
    public void A_note_closed_before_the_cat_arrives_is_not_collected()
    {
        var brain = CreateBrain();
        brain.NotesEnabled = false;
        bool collected = false;
        brain.NoteCollected += () => collected = true;
        brain.NoteToCollect = new Point(3700, 1000);

        Simulate(brain, 60, OnOtherMonitor, () =>
        {
            // The user closes the note the moment the cat sets off for it.
            if (brain.Mode == NyanMode.CollectNote) brain.NoteToCollect = null;
        });

        Assert.False(collected);
        Assert.False(brain.CarryingNote);
    }

    [Fact]
    public void Notes_are_dropped_inside_the_cats_screen()
    {
        var brain = CreateBrain();
        var drops = new List<Point>();
        brain.NoteDropped += drops.Add;

        Simulate(brain, 3600, OnOtherMonitor);

        Assert.NotEmpty(drops);
        Assert.All(drops, drop => Assert.True(Monitor.Contains(drop), $"{drop} is outside {Monitor}"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void No_notes_are_fetched_when_disabled_or_the_desktop_is_full(bool notesEnabled, bool slotAvailable)
    {
        var brain = CreateBrain();
        brain.NotesEnabled = notesEnabled;
        brain.NoteSlotAvailable = slotAvailable;
        int drops = 0;
        brain.NoteDropped += _ => drops++;

        Simulate(brain, 900, OnOtherMonitor, () => Assert.False(brain.CarryingNote));

        Assert.Equal(0, drops);
    }

    [Fact]
    public void Moving_to_another_screen_brings_the_cat_along_and_drops_the_old_trail()
    {
        var brain = CreateBrain();
        Simulate(brain, 1, OnOtherMonitor); // still on its first flight, so a trail exists
        Assert.NotEmpty(brain.Trail);
        var newScreen = new Rectangle(-2560, 200, 2560, 1440);

        brain.SetArea(newScreen);

        Assert.True(newScreen.Contains(Point.Round(brain.Position)), $"{brain.Position} is outside {newScreen}");
        Assert.Empty(brain.Trail);
        Assert.Empty(brain.Stars);
    }

    [Fact]
    public void Rainbow_trail_fades_out_behind_the_cat()
    {
        var brain = CreateBrain();
        bool everHadTrail = false;

        Simulate(brain, 120, OnOtherMonitor, () =>
        {
            everHadTrail |= brain.Trail.Count > 0;
            Assert.All(brain.Trail, s => Assert.InRange(brain.Time - s.Born, 0f, NyanBrain.TrailLife + Frame));
        });

        Assert.True(everHadTrail);
    }

    [Fact]
    public void Rainbow_has_no_gaps_when_the_cat_covers_a_lot_of_ground_in_one_frame()
    {
        var brain = CreateBrain();
        float spacing = Cat.Width / 4f;

        // One very slow frame: the cat flies several segment-lengths in a single update.
        brain.Update(0.5f, OnOtherMonitor);

        Assert.True(brain.Trail.Count >= 3, $"only {brain.Trail.Count} segments");
        var steps = brain.Trail.Zip(brain.Trail.Skip(1), (a, b) =>
            MathF.Sqrt(MathF.Pow(b.Position.X - a.Position.X, 2) + MathF.Pow(b.Position.Y - a.Position.Y, 2)));
        Assert.All(steps, step => Assert.InRange(step, spacing - 0.01f, spacing + 0.01f));
    }

    [Fact]
    public void Rainbow_follows_the_path_flown_whichever_way_the_cat_faces()
    {
        var brain = CreateBrain();
        var previous = brain.Position;
        int checkedSegments = 0;
        bool facedLeft = false, facedRight = false, looped = false;

        Simulate(brain, 600, OnOtherMonitor, () =>
        {
            var current = brain.Position;
            float moved = Length(previous, current);
            // A dash wraps round to the far edge in one frame; that jump isn't flown.
            if (moved < 100f)
            {
                foreach (var segment in brain.Trail.Where(s => s.Born == brain.Time))
                {
                    float detour = Length(previous, segment.Position) + Length(segment.Position, current) - moved;
                    Assert.True(detour < 0.1f, $"segment at {segment.Position} is off the path {previous} -> {current} ({brain.Mode})");
                    checkedSegments++;
                    facedLeft |= !brain.FacingRight;
                    facedRight |= brain.FacingRight;
                    looped |= brain.Mode == NyanMode.Loop;
                }
            }

            previous = current;
        });

        Assert.True(checkedSegments > 100 && facedLeft && facedRight && looped);

        static float Length(PointF a, PointF b) => MathF.Sqrt(MathF.Pow(b.X - a.X, 2) + MathF.Pow(b.Y - a.Y, 2));
    }

    [Fact]
    public void Touching_the_cat_with_the_cursor_makes_it_bolt_away()
    {
        var brain = CreateBrain();
        brain.MischiefEnabled = false;
        brain.NotesEnabled = false;
        Simulate(brain, 0.5f, OnOtherMonitor);
        Assert.Equal(NyanMode.Roam, brain.Mode);
        var poke = Point.Round(brain.Position);

        brain.Update(Frame, poke);
        Assert.Equal(NyanMode.Flee, brain.Mode);
        Simulate(brain, 0.3f, poke);

        float dx = brain.Position.X - poke.X, dy = brain.Position.Y - poke.Y;
        Assert.True(MathF.Sqrt(dx * dx + dy * dy) > 150f, $"cat only got to {brain.Position} from {poke}");
    }
}
