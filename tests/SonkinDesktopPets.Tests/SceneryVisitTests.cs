using System.Drawing;
using Xunit;

namespace SonkinDesktopPets.Tests;

/// <summary>What the pets get up to with the things in the scenery.</summary>
public class SceneryVisitTests
{
    // A secondary monitor to the right of the primary one, so (0,0)-based mistakes show up.
    private static readonly Rectangle Monitor = new(1920, 0, 1920, 1080);
    private static readonly Point Elsewhere = new(500, 500);
    private static readonly Size Sheep = new(78, 60);
    private static readonly Size Cat = new(136, 88);
    private static readonly Platform Ground = new(-1, 1920, 3840, 1040, 1920);
    private static readonly Platform Window = new(42, 2400, 3200, 600, 2400);
    private static readonly PointF Spot = new(2600, 1040);
    private static readonly PointF Perch = new(2600, 800);
    private const float Frame = 1f / 60f;

    private static SheepBrain SheepWith(SheepErrand errand, PointF spot, int seed = 1) =>
        new(new Random(seed), Sheep, Monitor, new[] { Ground, Window })
        {
            Attractions = new[] { new Attraction(errand, spot) },
        };

    private static NyanBrain CatWith(PointF perch, int seed = 1) =>
        new(new Random(seed), Cat, Monitor) { Perches = new[] { perch } };

    private static bool RunUntil(Action step, Func<bool> done, float seconds)
    {
        for (int i = 0; i < seconds / Frame; i++)
        {
            step();
            if (done()) return true;
        }

        return false;
    }

    [Theory]
    [InlineData(SheepErrand.Drink, SheepMode.Graze)]
    [InlineData(SheepErrand.Munch, SheepMode.Graze)]
    [InlineData(SheepErrand.Warm, SheepMode.Sit)]
    [InlineData(SheepErrand.Greet, SheepMode.Baa)]
    public void A_sheep_walks_up_beside_an_attraction_and_turns_to_it(SheepErrand errand, SheepMode doing)
    {
        var brain = SheepWith(errand, Spot);

        bool went = RunUntil(() => brain.Update(Frame, Elsewhere), () => brain.Errand == errand, 1800);

        Assert.True(went, $"the sheep never went to {errand}");
        Assert.Equal(doing, brain.Mode);
        Assert.Equal(Ground, brain.Standing);
        float offset = brain.Position.X - Spot.X;
        Assert.InRange(MathF.Abs(offset), Sheep.Width * 0.5f, Sheep.Width * 0.7f);
        Assert.Equal(offset < 0, brain.FacingRight);
    }

    [Fact]
    public void A_sheep_lies_down_right_in_the_shade()
    {
        var brain = SheepWith(SheepErrand.Rest, Spot);

        bool went = RunUntil(() => brain.Update(Frame, Elsewhere), () => brain.Errand == SheepErrand.Rest, 1800);

        Assert.True(went, "the sheep never went to lie in the shade");
        Assert.Equal(SheepMode.Sit, brain.Mode);
        Assert.InRange(brain.Position.X, Spot.X - 1f, Spot.X + 1f);
    }

    [Fact]
    public void The_errand_is_over_once_the_sheep_moves_on()
    {
        var brain = SheepWith(SheepErrand.Greet, Spot);
        RunUntil(() => brain.Update(Frame, Elsewhere), () => brain.Errand == SheepErrand.Greet, 1800);

        bool movedOn = RunUntil(() => brain.Update(Frame, Elsewhere), () => brain.Mode != SheepMode.Baa, 10);

        Assert.True(movedOn);
        Assert.Equal(SheepErrand.None, brain.Errand);
    }

    [Theory]
    [InlineData(500f, 1040f)] // on another screen's ground
    [InlineData(2600f, 600f)] // level with a window, not the ground
    public void A_sheep_ignores_attractions_it_cannot_walk_to(float x, float y)
    {
        var brain = SheepWith(SheepErrand.Munch, new PointF(x, y));

        bool went = RunUntil(() => brain.Update(Frame, Elsewhere), () => brain.Errand != SheepErrand.None, 900);

        Assert.False(went);
        Assert.True(Monitor.Left <= brain.Position.X && brain.Position.X <= Monitor.Right);
    }

    [Fact]
    public void The_cat_flies_to_a_perch_and_sits_still_on_top_of_it()
    {
        var brain = CatWith(Perch);

        bool landed = RunUntil(() => brain.Update(Frame, Elsewhere), () => brain.Perched, 600);

        Assert.True(landed, "the cat never perched");
        Assert.InRange(brain.Position.X, Perch.X - 1f, Perch.X + 1f);
        // Its feet are on the perch: its middle is a little under half a cat above it.
        Assert.InRange(brain.Position.Y, Perch.Y - Cat.Height / 2f, Perch.Y - Cat.Height / 4f);

        var seat = brain.Position;
        for (int i = 0; i < 3 / Frame; i++)
        {
            brain.Update(Frame, Elsewhere);
            Assert.True(brain.Perched, "the cat left its perch almost at once");
            Assert.Equal(seat, brain.Position);
        }
    }

    [Fact]
    public void A_perched_cat_bolts_when_poked()
    {
        var brain = CatWith(Perch);
        RunUntil(() => brain.Update(Frame, Elsewhere), () => brain.Perched, 600);

        brain.Update(Frame, Point.Round(brain.Position));

        Assert.Equal(NyanMode.Flee, brain.Mode);
    }

    [Fact]
    public void The_cat_does_other_things_between_perches()
    {
        var brain = CatWith(Perch);
        var gaps = new List<float>();
        float? leftAt = null;
        var previous = brain.Mode;

        RunUntil(() => brain.Update(Frame, Elsewhere), () =>
        {
            if (brain.Mode == NyanMode.Perch && previous != NyanMode.Perch && leftAt is { } left) gaps.Add(brain.Time - left);
            if (brain.Mode != NyanMode.Perch && previous == NyanMode.Perch) leftAt = brain.Time;
            previous = brain.Mode;
            return false;
        }, 3600);

        Assert.True(gaps.Count >= 5, $"only {gaps.Count + 1} perches in an hour");
        Assert.All(gaps, gap => Assert.True(gap >= NyanBrain.PerchCooldown - 0.1f, $"perched again after only {gap}s"));
    }

    [Fact]
    public void The_cat_leaves_perches_on_other_screens_alone()
    {
        var brain = CatWith(new PointF(500, 500));

        bool went = RunUntil(() => brain.Update(Frame, Elsewhere), () => brain.Mode == NyanMode.Perch, 900);

        Assert.False(went);
    }

    public static TheoryData<Scene> Landscapes => new() { Scene.Meadow, Scene.Forest, Scene.Mountains, Scene.Beach };

    [Theory]
    [MemberData(nameof(Landscapes))]
    public void Every_landscape_has_sights_and_they_are_where_something_is_drawn(Scene scene)
    {
        const int columns = 960;
        var landscape = Scenery.Compose(scene, columns, seed: 5);

        Assert.NotEmpty(landscape.Sights);
        Assert.All(landscape.Sights, sight =>
        {
            Assert.InRange(sight.X, 0, columns - 1);
            // A perch marks the tip of something; allow for a rounded top.
            bool drawn = landscape.Pixels[sight.X, sight.Y] is not null
                || landscape.Pixels[sight.X, Math.Min(sight.Y + 1, Scenery.Height - 1)] is not null;
            Assert.True(drawn, $"{sight} points at thin air");
        });
    }
}
