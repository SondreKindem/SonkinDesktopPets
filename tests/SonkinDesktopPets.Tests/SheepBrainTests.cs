using System.Drawing;
using Xunit;

namespace SonkinDesktopPets.Tests;

public class SheepBrainTests
{
    // A secondary monitor to the right of the primary one, so (0,0)-based mistakes show up.
    private static readonly Rectangle Monitor = new(1920, 0, 1920, 1080);
    private static readonly Point Elsewhere = new(500, 500);
    private static readonly Size Sheep = new(78, 60);
    private static readonly Platform Ground = new(-1, 1920, 3840, 1040, 1920);
    private static readonly Platform Window = new(42, 2400, 3200, 600, 2400);
    private const float Frame = 1f / 60f;

    private static SheepBrain CreateBrain(int seed = 1, params Platform[] platforms) =>
        new(new Random(seed), Sheep, Monitor, platforms.Length > 0 ? platforms : new[] { Ground });

    private static void Simulate(SheepBrain brain, float seconds, Point cursor, Action? afterEachFrame = null)
    {
        for (int i = 0; i < seconds / Frame; i++)
        {
            brain.Update(Frame, cursor);
            afterEachFrame?.Invoke();
        }
    }

    /// <summary>Runs until the sheep is standing calmly on the platform with the given id.</summary>
    private static void SimulateUntilSettledOn(SheepBrain brain, long platformId)
    {
        for (int i = 0; i < 3600 / Frame; i++)
        {
            brain.Update(Frame, Elsewhere);
            if (brain.Standing?.Id == platformId && brain.Mode is SheepMode.Sit or SheepMode.Idle or SheepMode.Baa) return;
        }

        Assert.Fail($"the sheep never settled on platform {platformId}");
    }

    [Fact]
    public void A_new_sheep_drops_in_from_above_and_lands_on_the_ground()
    {
        var brain = CreateBrain();
        Assert.True(brain.Position.Y < Monitor.Top, "the sheep should start above the screen");

        for (int i = 0; brain.Standing is null; i++)
        {
            Assert.True(i < 5 / Frame, "the sheep never landed");
            brain.Update(Frame, Elsewhere);
        }

        Assert.Equal(Ground, brain.Standing);
        Assert.Equal(Ground.Y, brain.Position.Y);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_sheep_on_its_feet_is_always_on_a_platform_and_gets_up_to_everything(int seed)
    {
        var brain = CreateBrain(seed, Ground, Window);
        var modesSeen = new HashSet<SheepMode>();
        var platformsVisited = new HashSet<long>();

        Simulate(brain, 1800, Elsewhere, () =>
        {
            modesSeen.Add(brain.Mode);
            Assert.True(Monitor.Left <= brain.Position.X && brain.Position.X <= Monitor.Right, $"{brain.Mode} took the sheep to {brain.Position}");
            if (brain.Standing is not { } platform) return;

            platformsVisited.Add(platform.Id);
            Assert.Equal(platform.Y, brain.Position.Y);
            Assert.True(platform.Spans(brain.Position.X), $"{brain.Mode}: standing at {brain.Position.X}, off the end of {platform}");
        });

        // Being startled and fleeing need the cursor, which stays away in this test.
        var spontaneous = Enum.GetValues<SheepMode>().Except(new[] { SheepMode.Startle, SheepMode.Flee });
        Assert.Equal(spontaneous.ToHashSet(), modesSeen);
        Assert.Equal(new HashSet<long> { Ground.Id, Window.Id }, platformsVisited);
    }

    [Fact]
    public void Grazing_happens_only_on_the_ground()
    {
        var brain = CreateBrain(1, Ground, Window);
        bool grazed = false, satOnWindow = false;

        Simulate(brain, 1800, Elsewhere, () =>
        {
            if (brain.Mode == SheepMode.Graze)
            {
                grazed = true;
                Assert.True(brain.Standing?.IsGround, "grazing on a window");
            }

            satOnWindow |= brain.Mode == SheepMode.Sit && brain.Standing?.Id == Window.Id;
        });

        Assert.True(grazed, "never grazed");
        Assert.True(satOnWindow, "never sat on the window");
    }

    [Fact]
    public void A_sheep_whose_window_closes_falls_to_the_ground()
    {
        var brain = CreateBrain(1, Ground, Window);
        SimulateUntilSettledOn(brain, Window.Id);
        float x = brain.Position.X;

        brain.SetWorld(new[] { Ground });
        brain.Update(Frame, Elsewhere);
        Assert.Equal(SheepMode.Fall, brain.Mode);
        Simulate(brain, 2, Elsewhere, () =>
        {
            // Straight down; it may wander off again once it has picked itself up.
            if (brain.Mode == SheepMode.Fall) Assert.Equal(x, brain.Position.X);
        });

        Assert.Equal(Ground.Id, brain.Standing?.Id);
    }

    [Fact]
    public void A_sheep_rides_along_when_its_window_is_dragged()
    {
        var brain = CreateBrain(1, Ground, Window);
        SimulateUntilSettledOn(brain, Window.Id);
        var before = brain.Position;

        var dragged = Window with { Left = Window.Left - 300, Right = Window.Right - 300, Anchor = Window.Anchor - 300, Y = Window.Y + 120 };
        brain.SetWorld(new[] { Ground, dragged });
        brain.Update(Frame, Elsewhere);

        Assert.Equal(dragged, brain.Standing);
        Assert.Equal(before.X - 300, brain.Position.X, 0.5f);
        Assert.Equal(dragged.Y, brain.Position.Y);
    }

    [Fact]
    public void A_sheep_falls_when_the_edge_under_it_gets_covered_even_though_the_window_remains()
    {
        var brain = CreateBrain(1, Ground, Window);
        SimulateUntilSettledOn(brain, Window.Id);

        // Another window now hides the part of the top edge the sheep is on; a stretch
        // well away from the sheep is still showing.
        bool sheepOnLeftHalf = brain.Position.X < 2800;
        var remaining = sheepOnLeftHalf ? Window with { Left = 3000 } : Window with { Right = 2600 };
        brain.SetWorld(new[] { Ground, remaining });
        Simulate(brain, 2, Elsewhere);

        Assert.Equal(Ground.Id, brain.Standing?.Id);
    }

    [Fact]
    public void A_sheep_that_falls_out_of_the_world_comes_back()
    {
        var brain = CreateBrain(1, Ground, Window);
        SimulateUntilSettledOn(brain, Window.Id);
        var newScreen = new Rectangle(-2560, 200, 2560, 1440);
        var newGround = new Platform(-1, -2560, 0, 1600, -2560);

        brain.SetArea(newScreen);
        brain.SetWorld(new[] { newGround });
        Simulate(brain, 10, Elsewhere);

        Assert.Equal(newGround.Id, brain.Standing?.Id);
        Assert.True(newGround.Spans(brain.Position.X), $"{brain.Position} is not on the new screen");
    }

    [Fact]
    public void Poking_a_sheep_with_the_cursor_makes_it_jump_and_run_away()
    {
        var brain = CreateBrain();
        SimulateUntilSettledOn(brain, Ground.Id);
        var poke = new Point((int)brain.Position.X, (int)brain.Position.Y - Sheep.Height / 2);

        brain.Update(Frame, poke);
        Assert.Equal(SheepMode.Startle, brain.Mode);
        bool leftTheGround = false;
        Simulate(brain, 2, poke, () => leftTheGround |= brain.Position.Y < Ground.Y - Sheep.Height / 4f);

        Assert.True(leftTheGround, "the sheep never jumped");
        Assert.True(Math.Abs(brain.Position.X - poke.X) > Sheep.Width * 1.5f, $"the sheep only got to {brain.Position} from {poke}");
    }

    [Fact]
    public void A_sleeping_sheep_is_woken_by_the_cursor()
    {
        var brain = CreateBrain(1, Ground, Window);
        for (int i = 0; brain.Mode != SheepMode.Sleep; i++)
        {
            Assert.True(i < 3600 / Frame, "the sheep never fell asleep");
            brain.Update(Frame, Elsewhere);
        }

        brain.Update(Frame, new Point((int)brain.Position.X, (int)brain.Position.Y - 5));

        Assert.NotEqual(SheepMode.Sleep, brain.Mode);
    }

    [Fact]
    public void Each_sheep_in_a_flock_goes_its_own_way()
    {
        var rng = new Random(1);
        var flock = Enumerable.Range(0, 4).Select(_ => new SheepBrain(rng, Sheep, Monitor, new[] { Ground, Window })).ToArray();

        for (int i = 0; i < 120 / Frame; i++)
        {
            foreach (var sheep in flock) sheep.Update(Frame, Elsewhere);
        }

        Assert.Equal(flock.Length, flock.Select(s => MathF.Round(s.Position.X)).Distinct().Count());
    }
}
