using System.Drawing;
using Xunit;

namespace SonkinDesktopPets.Tests;

public class SceneryTests
{
    public static TheoryData<Scene> Landscapes => new() { Scene.Meadow, Scene.Forest, Scene.Mountains, Scene.Beach };

    private static IEnumerable<Color?> Row(Color?[,] pixels, int row) =>
        Enumerable.Range(0, pixels.GetLength(0)).Select(x => pixels[x, row]);

    [Theory]
    [MemberData(nameof(Landscapes))]
    public void The_ground_runs_unbroken_along_the_bottom_where_the_sheep_walk(Scene scene)
    {
        var pixels = Scenery.Render(scene, columns: 641, seed: 3);

        Assert.All(Row(pixels, Scenery.Height - 1), pixel => Assert.NotNull(pixel));
    }

    [Theory]
    [MemberData(nameof(Landscapes))]
    public void The_top_of_the_strip_is_left_see_through(Scene scene)
    {
        var pixels = Scenery.Render(scene, columns: 641, seed: 3);

        Assert.All(Row(pixels, 0), pixel => Assert.Null(pixel));
    }

    [Theory]
    [MemberData(nameof(Landscapes))]
    public void A_screen_gets_the_same_landscape_every_time_it_is_redrawn(Scene scene)
    {
        var first = Scenery.Render(scene, columns: 300, seed: 11);
        var again = Scenery.Render(scene, columns: 300, seed: 11);
        var elsewhere = Scenery.Render(scene, columns: 300, seed: 12);

        Assert.Equal(first.Cast<Color?>(), again.Cast<Color?>());
        Assert.NotEqual(first.Cast<Color?>(), elsewhere.Cast<Color?>());
    }

    [Fact]
    public void No_scenery_draws_nothing()
    {
        Assert.All(Scenery.Render(Scene.None, columns: 100, seed: 1).Cast<Color?>(), pixel => Assert.Null(pixel));
    }

    [Theory]
    [InlineData(1920, 3, 640)]
    [InlineData(1921, 3, 641)] // a part-covered column at the edge still needs drawing
    [InlineData(2, 6, 1)]
    public void Enough_columns_are_drawn_to_reach_the_edge_of_the_screen(int width, int scale, int columns)
    {
        Assert.Equal(columns, Scenery.ColumnsFor(width, scale));
    }
}
