namespace SonkinDesktopPets;

/// <summary>A top-level window as the sheep see it.</summary>
public readonly record struct DesktopWindow(long Id, Rectangle Bounds, bool Maximized);

/// <summary>
/// A horizontal ledge a sheep can stand on: the bottom of a screen (above the taskbar) or the
/// visible part of a window's top edge. <see cref="Anchor"/> is the left edge of whatever the
/// ledge belongs to, so a sheep can tell how far its window has moved even when the visible
/// part of the edge changes.
/// </summary>
public readonly record struct Platform(long Id, int Left, int Right, int Y, int Anchor)
{
    public bool IsGround => Id < 0;

    public int Width => Right - Left;

    public bool Spans(float x) => x >= Left && x <= Right;
}

public static class Platforms
{
    /// <summary>
    /// Works out where sheep can stand. <paramref name="windows"/> are ordered topmost first;
    /// <paramref name="workAreas"/> are the usable parts of each screen in play.
    /// A window's top edge counts only where a sheep of height <paramref name="headroom"/>
    /// standing on it would be on screen and not in front of a window stacked above it.
    /// </summary>
    public static List<Platform> Compute(
        IReadOnlyList<DesktopWindow> windows, IReadOnlyList<Rectangle> workAreas, int headroom, int minWidth)
    {
        var platforms = new List<Platform>();
        for (int i = 0; i < workAreas.Count; i++)
        {
            var area = workAreas[i];
            platforms.Add(new Platform(-(i + 1), area.Left, area.Right, area.Bottom, area.Left));
        }

        var segments = new List<(int Left, int Right)>();
        for (int i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            if (window.Maximized) continue;
            int y = window.Bounds.Top;

            foreach (var area in workAreas)
            {
                if (y - headroom < area.Top || y >= area.Bottom) continue;
                int left = Math.Max(window.Bounds.Left, area.Left), right = Math.Min(window.Bounds.Right, area.Right);
                if (right - left < minWidth) continue;

                segments.Clear();
                segments.Add((left, right));
                for (int above = 0; above < i && segments.Count > 0; above++)
                {
                    var cover = windows[above].Bounds;
                    if (cover.Top <= y && cover.Bottom > y - headroom) Subtract(segments, cover.Left, cover.Right);
                }

                foreach (var (l, r) in segments)
                {
                    if (r - l >= minWidth) platforms.Add(new Platform(window.Id, l, r, y, window.Bounds.Left));
                }
            }
        }

        return platforms;
    }

    private static void Subtract(List<(int Left, int Right)> segments, int left, int right)
    {
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            var (l, r) = segments[i];
            if (right <= l || left >= r) continue;
            segments.RemoveAt(i);
            if (left > l) segments.Add((l, left));
            if (right < r) segments.Add((right, r));
        }
    }
}
