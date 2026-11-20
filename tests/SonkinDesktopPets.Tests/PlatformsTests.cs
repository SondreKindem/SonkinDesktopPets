using System.Drawing;
using Xunit;

namespace SonkinDesktopPets.Tests;

public class PlatformsTests
{
    // The usable part of a 1920x1080 screen with a 40 pixel taskbar along the bottom.
    private static readonly Rectangle WorkArea = new(0, 0, 1920, 1040);
    private const int Headroom = 60;
    private const int MinWidth = 78;

    private static List<Platform> Compute(params DesktopWindow[] topmostFirst) =>
        Platforms.Compute(topmostFirst, new[] { WorkArea }, Headroom, MinWidth);

    private static Platform[] WindowLedges(IEnumerable<Platform> platforms) =>
        platforms.Where(p => !p.IsGround).OrderBy(p => p.Left).ToArray();

    [Fact]
    public void The_ground_is_the_top_of_the_taskbar_on_every_screen()
    {
        var second = new Rectangle(1920, 200, 1280, 984);

        var platforms = Platforms.Compute(Array.Empty<DesktopWindow>(), new[] { WorkArea, second }, Headroom, MinWidth);

        Assert.Equal(2, platforms.Count);
        Assert.All(platforms, p => Assert.True(p.IsGround));
        Assert.Contains(platforms, p => p is { Left: 0, Right: 1920, Y: 1040 });
        Assert.Contains(platforms, p => p is { Left: 1920, Right: 3200, Y: 1184 });
        Assert.Equal(2, platforms.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void A_free_standing_window_offers_its_whole_top_edge()
    {
        var ledge = Assert.Single(WindowLedges(Compute(new DesktopWindow(7, new Rectangle(300, 400, 800, 500), false))));

        Assert.Equal(new Platform(7, 300, 1100, 400, 300), ledge);
    }

    [Fact]
    public void A_maximized_window_is_nothing_to_stand_on()
    {
        var maximized = new DesktopWindow(7, new Rectangle(0, 0, 1920, 1040), true);

        Assert.Empty(WindowLedges(Compute(maximized)));
    }

    [Fact]
    public void A_maximized_window_hides_the_windows_behind_it()
    {
        var maximized = new DesktopWindow(7, new Rectangle(0, 0, 1920, 1040), true);
        var behind = new DesktopWindow(8, new Rectangle(300, 400, 800, 500), false);

        Assert.Empty(WindowLedges(Compute(maximized, behind)));
        Assert.Single(WindowLedges(Compute(behind, maximized)));
    }

    [Fact]
    public void A_window_in_front_cuts_its_width_out_of_the_edge_behind_it()
    {
        var front = new DesktopWindow(1, new Rectangle(600, 300, 200, 400), false);
        var behind = new DesktopWindow(2, new Rectangle(300, 400, 800, 500), false);

        var ledges = WindowLedges(Compute(front, behind)).Where(p => p.Id == 2).ToArray();

        Assert.Equal(2, ledges.Length);
        Assert.Equal((300, 600), (ledges[0].Left, ledges[0].Right));
        Assert.Equal((800, 1100), (ledges[1].Left, ledges[1].Right));
        // Both pieces still report where the window itself starts.
        Assert.All(ledges, p => Assert.Equal(300, p.Anchor));
    }

    [Fact]
    public void A_window_in_front_that_only_covers_the_space_above_the_edge_still_blocks_it()
    {
        // It ends just above the lower window's top edge, right where a sheep would stand.
        var front = new DesktopWindow(1, new Rectangle(300, 100, 800, 290), false);
        var behind = new DesktopWindow(2, new Rectangle(300, 400, 800, 500), false);

        Assert.DoesNotContain(Compute(front, behind), p => p.Id == 2);
    }

    [Fact]
    public void A_window_in_front_but_clear_of_the_edge_does_not_block_it()
    {
        var front = new DesktopWindow(1, new Rectangle(300, 600, 800, 300), false);
        var behind = new DesktopWindow(2, new Rectangle(300, 400, 800, 500), false);

        Assert.Contains(Compute(front, behind), p => p is { Id: 2, Left: 300, Right: 1100 });
    }

    [Theory]
    [InlineData(30)] // too close to the top of the screen for a sheep to fit
    [InlineData(-200)] // top edge off the top of the screen
    [InlineData(1060)] // top edge below the ground
    public void An_edge_with_no_room_for_a_sheep_on_screen_is_left_out(int top)
    {
        Assert.Empty(WindowLedges(Compute(new DesktopWindow(7, new Rectangle(300, top, 800, 500), false))));
    }

    [Fact]
    public void Leftover_slivers_too_narrow_for_a_sheep_are_left_out()
    {
        var front = new DesktopWindow(1, new Rectangle(350, 300, 700, 400), false);
        var behind = new DesktopWindow(2, new Rectangle(300, 400, 800, 500), false);

        Assert.DoesNotContain(Compute(front, behind), p => p.Id == 2);
    }

    [Fact]
    public void A_window_hanging_off_the_screen_offers_only_the_part_on_screen()
    {
        var ledge = Assert.Single(WindowLedges(Compute(new DesktopWindow(7, new Rectangle(1500, 400, 800, 500), false))));

        Assert.Equal((1500, 1920), (ledge.Left, ledge.Right));
    }
}
