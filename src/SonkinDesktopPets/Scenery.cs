using System.Drawing.Imaging;

namespace SonkinDesktopPets;

public enum Scene
{
    None,
    Meadow,
    Forest,
    Mountains,
    Beach,
}

/// <summary>
/// Pixel-art landscapes for the bottom of the screen, generated in code like the pets. A scene
/// is a strip as wide as the screen whose bottom row is the ground the sheep walk on; whatever
/// it leaves empty stays see-through.
/// <para>
/// Each is built back to front from rolling layers of land. Small things are scattered over a
/// layer in clumps rather than evenly, and every so often a one-off landmark is set down, so
/// that no two stretches look alike.
/// </para>
/// </summary>
public static class Scenery
{
    /// <summary>How tall a scene is, in art pixels.</summary>
    public const int Height = 64;

    public static readonly (Scene Scene, string Label)[] Choices =
    {
        (Scene.None, "None"),
        (Scene.Meadow, "Meadow"),
        (Scene.Forest, "Forest"),
        (Scene.Mountains, "Mountains"),
        (Scene.Beach, "Beach"),
    };

    private static readonly Color[] Petals =
    {
        Color.FromArgb(255, 255, 255), Color.FromArgb(255, 226, 92), Color.FromArgb(255, 150, 190),
        Color.FromArgb(236, 84, 84), Color.FromArgb(170, 130, 240),
    };

    private static readonly Color Bark = Color.FromArgb(120, 86, 58);
    private static readonly Color BarkDark = Color.FromArgb(92, 64, 44);
    private static readonly Color Water = Color.FromArgb(72, 156, 216);
    private static readonly Color WaterLight = Color.FromArgb(150, 210, 240);
    private static readonly Color Stone = Color.FromArgb(150, 154, 166);
    private static readonly Color StoneLight = Color.FromArgb(186, 190, 200);
    private static readonly Color StoneDark = Color.FromArgb(112, 116, 132);

    /// <summary>Foliage as (body, lit side, shaded underside).</summary>
    private static readonly (Color Body, Color Light, Color Dark) Leaf =
        (Color.FromArgb(70, 150, 84), Color.FromArgb(104, 180, 100), Color.FromArgb(52, 122, 76));

    private static readonly (Color Body, Color Light, Color Dark) DeepLeaf =
        (Color.FromArgb(52, 128, 80), Color.FromArgb(80, 156, 92), Color.FromArgb(38, 102, 70));

    private static readonly (Color Body, Color Light, Color Dark) Blossom =
        (Color.FromArgb(244, 170, 200), Color.FromArgb(255, 208, 226), Color.FromArgb(214, 130, 170));

    private static readonly (Color Body, Color Light, Color Dark) Autumn =
        (Color.FromArgb(230, 140, 60), Color.FromArgb(246, 184, 84), Color.FromArgb(190, 100, 50));

    /// <summary>Draws one thing at column x, standing on the surface row y.</summary>
    private delegate void Feature(Canvas c, Random rng, int x, int y);

    /// <summary>How many art pixels it takes to span <paramref name="width"/> screen pixels.</summary>
    public static int ColumnsFor(int width, int scale) => (width + scale - 1) / scale;

    /// <summary>
    /// The scene as a grid of art pixels, [column, row] with row 0 at the top; null is empty.
    /// The same <paramref name="seed"/> always gives the same landscape.
    /// </summary>
    public static Color?[,] Render(Scene scene, int columns, int seed)
    {
        var canvas = new Canvas(Math.Max(columns, 0));
        var rng = new Random(seed);
        switch (scene)
        {
            case Scene.Meadow: Meadow(canvas, rng); break;
            case Scene.Forest: Forest(canvas, rng); break;
            case Scene.Mountains: Mountains(canvas, rng); break;
            case Scene.Beach: Beach(canvas, rng); break;
        }

        return canvas.Pixels;
    }

    /// <summary>The scene drawn <paramref name="width"/> screen pixels wide, each art pixel a block of <paramref name="scale"/>.</summary>
    internal static Bitmap Build(Scene scene, int width, int scale, int seed)
    {
        int columns = ColumnsFor(width, scale);
        var pixels = Render(scene, columns, seed);
        var bitmap = new Bitmap(Math.Max(width, 1), Height * scale, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.Black);
        for (int y = 0; y < Height; y++)
        {
            // Paint each run of one colour in a single go: a landscape is mostly long runs.
            for (int x = 0; x < columns;)
            {
                var colour = pixels[x, y];
                int end = x + 1;
                while (end < columns && pixels[end, y] == colour) end++;
                if (colour is { } c)
                {
                    brush.Color = c;
                    g.FillRectangle(brush, x * scale, y * scale, (end - x) * scale, scale);
                }

                x = end;
            }
        }

        return bitmap;
    }

    // ---- The scenes ---------------------------------------------------------------------

    private static void Meadow(Canvas c, Random rng)
    {
        const int far = 0, mid = 1, front = 2;
        var marks = new Landmarks(rng, c.Width,
            (far, Windmill), (mid, Cottage), (mid, Pond), (mid, Haystacks), (mid, Orchard),
            (front, Fence), (front, FlowerBed), (front, Boulder));

        Birds(c, rng, Color.FromArgb(70, 74, 92));

        var farHills = Rolling(rng, 27, 9, 150);
        c.Land(farHills, Color.FromArgb(132, 198, 126), Color.FromArgb(158, 216, 146));
        foreach (int x in Clumps(rng, c.Width, 14, 40, 260))
        {
            if (marks.Near(far, x)) continue;
            int y = c.Top(farHills, x);
            if (rng.Next(4) == 0) Poplar(c, rng, x, y);
            else RoundTree(c, rng, x, y, rng.Next(9) == 0 ? Blossom : rng.Next(3) == 0 ? DeepLeaf : Leaf);
        }

        marks.Draw(c, rng, far, farHills);

        var midHills = Rolling(rng, 15, 5, 85);
        c.Land(midHills, Color.FromArgb(100, 180, 90), Color.FromArgb(128, 204, 108));
        foreach (int x in Clumps(rng, c.Width, 10, 30, 180))
        {
            if (!marks.Near(mid, x, 16)) Bush(c, rng, x, c.Top(midHills, x), rng.Next(3) == 0 ? Leaf : DeepLeaf);
        }

        marks.Draw(c, rng, mid, midHills);

        var ground = Rolling(rng, 6, 1.5f, 50);
        var blade = Color.FromArgb(116, 206, 88);
        c.Land(ground, Color.FromArgb(80, 166, 66), blade);
        foreach (int x in Every(rng, c.Width, 5, 14)) Tuft(c, x, c.Top(ground, x), blade);
        // Flowers come in drifts of one colour, with bare grass between.
        var drift = Petals[0];
        foreach (int x in Clumps(rng, c.Width, 2, 7, 120))
        {
            if (rng.Next(6) == 0) drift = Petals[rng.Next(Petals.Length)];
            Flower(c, rng, x, c.Top(ground, x), rng.Next(4) == 0 ? Petals[rng.Next(Petals.Length)] : drift);
        }

        marks.Draw(c, rng, front, ground);
    }

    private static void Forest(Canvas c, Random rng)
    {
        const int mid = 1, front = 2;
        var marks = new Landmarks(rng, c.Width,
            (mid, Cabin), (mid, Camp), (mid, OldOak), (mid, BirchStand), (mid, Pond),
            (front, FallenLog), (front, Deer), (front, Boulder), (front, Stumps));

        // A hazy wall of firs at the back, thinning out in places.
        var back = Rolling(rng, 13, 3, 90);
        Color hazeLight = Color.FromArgb(44, 96, 88), hazeDark = Color.FromArgb(34, 80, 76);
        foreach (int x in Clumps(rng, c.Width, 4, 9, 210, sparse: 0.35f))
        {
            Pine(c, x, c.Top(back, x) + 2, rng.Next(22, 46), hazeLight, hazeDark, null);
        }

        c.Land(back, hazeDark, hazeLight);

        var midLand = Rolling(rng, 9, 2, 70);
        c.Land(midLand, Color.FromArgb(42, 104, 64), Color.FromArgb(58, 124, 74));
        foreach (int x in Clumps(rng, c.Width, 8, 20, 150))
        {
            if (marks.Near(mid, x, 15)) continue;
            int y = c.Top(midLand, x);
            switch (rng.Next(10))
            {
                case 0: RoundTree(c, rng, x, y, rng.Next(3) == 0 ? Autumn : DeepLeaf); break;
                case 1: Birch(c, rng, x, y); break;
                default:
                    // Now and then a giant stands over the rest.
                    int height = rng.Next(12) == 0 ? rng.Next(40, 48) : rng.Next(18, 36);
                    Pine(c, x, y + 2, height, Color.FromArgb(58, 136, 82), Color.FromArgb(40, 106, 70), Color.FromArgb(104, 72, 48));
                    break;
            }
        }

        marks.Draw(c, rng, mid, midLand);

        var ground = Rolling(rng, 6, 1.5f, 55);
        var blade = Color.FromArgb(90, 164, 82);
        c.Land(ground, Color.FromArgb(58, 126, 64), blade);
        foreach (int x in Every(rng, c.Width, 6, 16)) Tuft(c, x, c.Top(ground, x), blade);
        foreach (int x in Clumps(rng, c.Width, 5, 16, 140))
        {
            int y = c.Top(ground, x);
            if (rng.Next(3) == 0) Fern(c, x, y);
            else Mushroom(c, rng, x, y);
        }

        marks.Draw(c, rng, front, ground);
    }

    private static void Mountains(Canvas c, Random rng)
    {
        const int mid = 1, front = 2;
        var marks = new Landmarks(rng, c.Width,
            (mid, Lake), (mid, Cabin), (mid, Grove), (mid, Lake),
            (front, Boulder), (front, FallenLog), (front, Deer), (front, FlowerBed));

        Clouds(c, rng);
        Range(c, rng, 40, Height - 3, snowFrom: 46,
            Color.FromArgb(138, 148, 174), Color.FromArgb(110, 120, 150));
        // A nearer, lower, darker range in front of the high peaks.
        Range(c, rng, 24, 38, snowFrom: int.MaxValue,
            Color.FromArgb(98, 112, 136), Color.FromArgb(78, 92, 118));

        var foothills = Rolling(rng, 19, 5, 95);
        c.Land(foothills, Color.FromArgb(72, 130, 96), Color.FromArgb(94, 152, 112));
        foreach (int x in Clumps(rng, c.Width, 5, 16, 170))
        {
            if (marks.Near(mid, x, 18)) continue;
            Pine(c, x, c.Top(foothills, x) + 2, rng.Next(8, 17), Color.FromArgb(50, 112, 82), Color.FromArgb(36, 90, 70), null);
        }

        marks.Draw(c, rng, mid, foothills);

        var ground = Rolling(rng, 7, 2, 60);
        var blade = Color.FromArgb(124, 200, 100);
        c.Land(ground, Color.FromArgb(92, 168, 84), blade);
        foreach (int x in Every(rng, c.Width, 6, 16)) Tuft(c, x, c.Top(ground, x), blade);
        foreach (int x in Clumps(rng, c.Width, 6, 20, 130))
        {
            int y = c.Top(ground, x);
            if (rng.Next(4) == 0) Pebble(c, x, y);
            else Flower(c, rng, x, y, Petals[rng.Next(3)]);
        }

        marks.Draw(c, rng, front, ground);
    }

    private static void Beach(Canvas c, Random rng)
    {
        const int sea = 0, sand = 1;
        const int seaLevel = 19;
        var marks = new Landmarks(rng, c.Width,
            (sea, Sailboat), (sea, Island), (sea, SeaRocks), (sea, Lighthouse), (sea, Sailboat),
            (sand, Parasol), (sand, Sandcastle), (sand, Driftwood), (sand, BeachBall));

        Birds(c, rng, Color.FromArgb(236, 240, 246));

        c.Land(_ => seaLevel, Water, Color.FromArgb(128, 200, 238));
        // Short glints of light on the water.
        for (int i = 0; i < c.Width / 5; i++)
        {
            int x = rng.Next(c.Width), y = Height - rng.Next(8, seaLevel - 1), length = rng.Next(2, 5);
            for (int k = 0; k < length; k++) c.Put(x + k, y, WaterLight);
        }

        marks.Draw(c, rng, sea, _ => seaLevel);

        var dunes = Rolling(rng, 9, 3.5f, 120);
        foreach (int x in Clumps(rng, c.Width, 16, 48, 300))
        {
            if (!marks.Near(sea, x, 16)) Palm(c, rng, x, c.Top(dunes, x) + 1, rng.Next(15, 27));
        }

        Color sandColour = Color.FromArgb(240, 214, 150), speck = Color.FromArgb(222, 192, 128);
        // Surf where the water meets the sand.
        var surf = Color.FromArgb(232, 246, 252);
        for (int x = 0; x < c.Width; x++)
        {
            int top = c.Top(dunes, x);
            if (c.Pixels[x, top - 1] == Water || c.Pixels[x, top - 1] == WaterLight) c.Put(x, top - 1, surf);
        }

        c.Land(dunes, sandColour, Color.FromArgb(250, 234, 184));
        for (int i = 0; i < c.Width / 2; i++)
        {
            int x = rng.Next(c.Width), y = Height - 1 - rng.Next(6);
            if (c.Pixels[x, y] == sandColour) c.Put(x, y, speck);
        }

        foreach (int x in Clumps(rng, c.Width, 6, 20, 160)) Tuft(c, x, c.Top(dunes, x), Color.FromArgb(120, 170, 96));
        foreach (int x in Clumps(rng, c.Width, 10, 34, 110))
        {
            int y = Height - 2 - rng.Next(3);
            switch (rng.Next(4))
            {
                case 0: Crab(c, x, y); break;
                case 1: Shell(c, x, y); break;
                default: Starfish(c, rng, x, y); break;
            }
        }

        marks.Draw(c, rng, sand, dunes);
    }

    // ---- Laying things out ----------------------------------------------------------------

    /// <summary>A gently rolling skyline: how high the land stands above the bottom at each column.</summary>
    private static Func<int, int> Rolling(Random rng, float height, float swell, float wavelength)
    {
        float first = (float)rng.NextDouble() * MathF.Tau, second = (float)rng.NextDouble() * MathF.Tau;
        return x => (int)MathF.Round(height
            + swell * (0.65f * MathF.Sin(first + x * MathF.Tau / wavelength)
                + 0.35f * MathF.Sin(second + x * MathF.Tau / (wavelength * 0.37f))));
    }

    /// <summary>Random spots along the strip, between <paramref name="min"/> and <paramref name="max"/> apart.</summary>
    private static List<int> Every(Random rng, int columns, int min, int max)
    {
        var spots = new List<int>();
        for (int x = rng.Next(max); x < columns; x += rng.Next(min, max + 1)) spots.Add(x);
        return spots;
    }

    /// <summary>
    /// Like <see cref="Every"/>, but thinned out in slow waves about <paramref name="wavelength"/>
    /// long, leaving thickets and clearings. <paramref name="sparse"/> is how much survives in
    /// the barest stretch.
    /// </summary>
    private static List<int> Clumps(Random rng, int columns, int min, int max, float wavelength, float sparse = 0f)
    {
        float first = (float)rng.NextDouble() * MathF.Tau, second = (float)rng.NextDouble() * MathF.Tau;
        var spots = new List<int>();
        foreach (int x in Every(rng, columns, min, max))
        {
            float wave = 0.5f + 0.35f * MathF.Sin(first + x * MathF.Tau / wavelength)
                + 0.15f * MathF.Sin(second + x * MathF.Tau / (wavelength * 0.41f));
            if (rng.NextDouble() < sparse + (1 - sparse) * wave * wave * 1.6f) spots.Add(x);
        }

        return spots;
    }

    /// <summary>
    /// The one-off sights of a scene. Spots are picked well apart along the strip and each is
    /// dealt a feature from a shuffled deck, so that the whole deck comes round before any repeats.
    /// A feature belongs to one layer of land and is drawn when that layer is.
    /// </summary>
    private sealed class Landmarks
    {
        private readonly List<(int Layer, Feature Draw, int X)> _placed = new();

        public Landmarks(Random rng, int columns, params (int Layer, Feature Draw)[] deck)
        {
            var pile = new List<(int Layer, Feature Draw)>();
            for (int x = rng.Next(25, 100); x < columns - 12; x += rng.Next(70, 140))
            {
                if (pile.Count == 0) pile.AddRange(deck);
                int pick = rng.Next(pile.Count);
                _placed.Add((pile[pick].Layer, pile[pick].Draw, x));
                pile.RemoveAt(pick);
            }
        }

        /// <summary>True if a landmark of this layer stands within <paramref name="reach"/> of column x.</summary>
        public bool Near(int layer, int x, int reach = 12) =>
            _placed.Any(mark => mark.Layer == layer && Math.Abs(mark.X - x) <= reach);

        public void Draw(Canvas c, Random rng, int layer, Func<int, int> land)
        {
            foreach (var mark in _placed)
            {
                if (mark.Layer == layer) mark.Draw(c, rng, mark.X, c.Top(land, mark.X));
            }
        }
    }

    // ---- Sky ------------------------------------------------------------------------------

    private static void Birds(Canvas c, Random rng, Color colour)
    {
        foreach (int x in Every(rng, c.Width, 90, 260))
        {
            int y = rng.Next(6, 22);
            for (int i = rng.Next(1, 4); i > 0; i--)
            {
                int bx = x + i * rng.Next(5, 9), by = y + rng.Next(-3, 4);
                c.Put(bx - 2, by, colour);
                c.Put(bx - 1, by - 1, colour);
                c.Put(bx, by, colour);
                c.Put(bx + 1, by - 1, colour);
                c.Put(bx + 2, by, colour);
            }
        }
    }

    private static void Clouds(Canvas c, Random rng)
    {
        Color cloud = Color.FromArgb(225, 255, 255, 255), shade = Color.FromArgb(225, 222, 230, 244);
        foreach (int x in Every(rng, c.Width, 60, 170))
        {
            int y = rng.Next(9, 24);
            float size = 3f + (float)rng.NextDouble() * 2.5f;
            c.Blob(x - size * 1.6f, y + 1, size * 1.4f, size * 0.7f, cloud, cloud, shade);
            c.Blob(x + size * 1.5f, y + 1, size * 1.6f, size * 0.7f, cloud, cloud, shade);
            c.Blob(x, y - 1, size * 1.5f, size, cloud, cloud, shade);
        }
    }

    /// <summary>A jagged range of peaks, each with slopes of its own, lit from the left.</summary>
    private static void Range(Canvas c, Random rng, int lowest, int highest, int snowFrom, Color rock, Color shade)
    {
        Color snow = Color.FromArgb(244, 247, 252), snowShade = Color.FromArgb(208, 218, 236);
        var peaks = new List<(int X, int Height, float Left, float Right)>();
        for (int x = rng.Next(-10, 20); x < c.Width + 30; x += rng.Next(22, 62))
        {
            peaks.Add((x, rng.Next(lowest, highest + 1), 0.7f + (float)rng.NextDouble() * 0.8f, 0.7f + (float)rng.NextDouble() * 0.8f));
        }

        int crag = 0;
        for (int x = 0; x < c.Width; x++)
        {
            // Each column belongs to whichever peak stands tallest over it.
            float Over((int X, int Height, float Left, float Right) p) =>
                p.Height - (x < p.X ? (p.X - x) * p.Left : (x - p.X) * p.Right);
            var peak = peaks.MaxBy(Over);
            // The ridge line wobbles a pixel either way, a few columns at a time.
            if (x % 3 == 0) crag = rng.Next(-1, 2);
            int height = Math.Clamp((int)MathF.Round(Over(peak)) + (x == peak.X ? 0 : crag), 8, Height - 2);
            bool shaded = x > peak.X;
            // The snow line zigzags, and only the taller peaks reach it.
            int snowDepth = peak.Height >= snowFrom ? 5 + (x % 4) switch { 0 => 0, 1 => 2, 2 => 3, _ => 1 } + (peak.Height - snowFrom) / 4 : 0;
            for (int h = 1; h <= height; h++)
            {
                bool snowy = peak.Height - h < snowDepth;
                c.Put(x, Height - h, snowy ? (shaded ? snowShade : snow) : shaded ? shade : rock);
            }
        }
    }

    // ---- Plants ---------------------------------------------------------------------------

    /// <summary>A fir in tiers, lit from the left. <paramref name="y"/> is the row just below its foot.</summary>
    private static void Pine(Canvas c, int x, int y, int height, Color light, Color dark, Color? trunk)
    {
        int trunkHeight = trunk is null ? 0 : 4;
        int canopy = height - trunkHeight;
        int tier = Math.Max(canopy / 3, 2);
        float spread = canopy / 15f;
        for (int row = 0; row < canopy; row++)
        {
            int level = Math.Min(row / tier, 2);
            float within = (row - level * tier) / (float)Math.Max(tier - 1, 1);
            int half = (int)MathF.Round(spread * (level + within * (2 + level)));
            for (int dx = -half; dx <= half; dx++) c.Put(x + dx, y - height + row, dx <= 0 ? light : dark);
        }

        if (trunk is { } bark)
        {
            for (int row = 1; row <= trunkHeight; row++)
            {
                c.Put(x, y - row, bark);
                c.Put(x + 1, y - row, bark);
            }
        }
    }

    private static void RoundTree(Canvas c, Random rng, int x, int y, (Color Body, Color Light, Color Dark) leaf, float size = 1f)
    {
        int trunk = (int)(rng.Next(4, 8) * size);
        float r = (4f + (float)rng.NextDouble() * 2.5f) * size;
        for (int row = 0; row <= trunk; row++)
        {
            c.Put(x, y - row + 1, Bark);
            c.Put(x + 1, y - row + 1, size > 1.3f ? BarkDark : Bark);
            if (size > 1.3f) c.Put(x - 1, y - row + 1, Bark);
        }

        float top = y - trunk - r * 0.7f;
        c.Blob(x - r * 0.7f, top + r * 0.35f, r * 0.75f, r * 0.75f, leaf.Body, leaf.Light, leaf.Dark);
        c.Blob(x + 1 + r * 0.7f, top + r * 0.35f, r * 0.75f, r * 0.75f, leaf.Body, leaf.Light, leaf.Dark);
        c.Blob(x + 0.5f, top, r, r, leaf.Body, leaf.Light, leaf.Dark);
    }

    /// <summary>A tall, narrow tree, the sort planted in rows along country roads.</summary>
    private static void Poplar(Canvas c, Random rng, int x, int y)
    {
        int height = rng.Next(12, 19);
        c.Put(x, y, Bark);
        c.Put(x, y - 1, Bark);
        c.Blob(x, y - 1 - height / 2f, 2.3f, height / 2f, DeepLeaf.Body, DeepLeaf.Light, DeepLeaf.Dark);
    }

    private static void Birch(Canvas c, Random rng, int x, int y)
    {
        int trunk = rng.Next(12, 20);
        Color white = Color.FromArgb(236, 236, 228), mark = Color.FromArgb(70, 66, 64);
        for (int row = 0; row <= trunk; row++)
        {
            c.Put(x, y - row + 1, row % 4 == 1 ? mark : white);
            c.Put(x + 1, y - row + 1, row % 4 == 3 ? mark : Color.FromArgb(206, 208, 204));
        }

        var leaf = (Color.FromArgb(132, 190, 84), Color.FromArgb(170, 214, 104), Color.FromArgb(98, 158, 74));
        c.Blob(x - 2.5f, y - trunk + 2, 3f, 2.6f, leaf.Item1, leaf.Item2, leaf.Item3);
        c.Blob(x + 3.5f, y - trunk + 1, 3f, 2.6f, leaf.Item1, leaf.Item2, leaf.Item3);
        c.Blob(x + 0.5f, y - trunk - 2, 3.6f, 3.2f, leaf.Item1, leaf.Item2, leaf.Item3);
    }

    private static void Bush(Canvas c, Random rng, int x, int y, (Color Body, Color Light, Color Dark) leaf)
    {
        float r = 2.5f + (float)rng.NextDouble() * 1.5f;
        c.Blob(x, y - r * 0.4f, r, r, leaf.Body, leaf.Light, leaf.Dark);
        c.Blob(x + r * 1.2f, y - r * 0.2f, r * 0.8f, r * 0.8f, leaf.Body, leaf.Light, leaf.Dark);
        // Some are in berry.
        if (rng.Next(4) == 0)
        {
            var berry = Color.FromArgb(226, 70, 84);
            c.Put(x - 1, y - (int)r, berry);
            c.Put(x + 1, y - 1, berry);
            c.Put(x + (int)r, y - 2, berry);
        }
    }

    /// <summary>Three blades of grass poking up from the row <paramref name="y"/>.</summary>
    private static void Tuft(Canvas c, int x, int y, Color blade)
    {
        c.Put(x, y - 1, blade);
        c.Put(x + 1, y - 2, blade);
        c.Put(x + 1, y - 1, blade);
        c.Put(x + 2, y - 1, blade);
    }

    private static void Fern(Canvas c, int x, int y)
    {
        var frond = Color.FromArgb(110, 186, 96);
        for (int i = 0; i <= 3; i++)
        {
            c.Put(x - i, y - 3 + i * i / 3, frond);
            c.Put(x + i, y - 3 + i * i / 3, frond);
        }

        c.Put(x, y - 1, frond);
        c.Put(x, y - 2, frond);
    }

    private static void Flower(Canvas c, Random rng, int x, int y, Color petal)
    {
        var stem = Color.FromArgb(52, 130, 60);
        if (rng.Next(3) == 0)
        {
            // A big daisy: four petals around a middle.
            c.Put(x, y - 1, stem);
            c.Put(x, y - 2, stem);
            c.Put(x, y - 3, petal);
            c.Put(x - 1, y - 4, petal);
            c.Put(x + 1, y - 4, petal);
            c.Put(x, y - 5, petal);
            c.Put(x, y - 4, petal == Petals[1] ? Petals[0] : Petals[1]);
        }
        else
        {
            c.Put(x, y - 1, stem);
            c.Put(x, y - 2, petal);
        }
    }

    private static void Mushroom(Canvas c, Random rng, int x, int y)
    {
        var stalk = Color.FromArgb(240, 230, 206);
        var cap = rng.Next(3) == 0 ? Color.FromArgb(196, 140, 84) : Color.FromArgb(214, 62, 56);
        c.Put(x + 1, y, stalk);
        c.Put(x + 1, y - 1, stalk);
        for (int dx = 0; dx <= 2; dx++) c.Put(x + dx, y - 2, cap);
        c.Put(x + 1, y - 3, cap);
        c.Put(x, y - 2, Color.White);
    }

    private static void Palm(Canvas c, Random rng, int x, int y, int height)
    {
        int lean = rng.Next(2) == 0 ? -1 : 1;
        Color bark = Color.FromArgb(156, 112, 68), ring = Color.FromArgb(126, 88, 52);
        Color frond = Color.FromArgb(66, 168, 88), under = Color.FromArgb(42, 128, 72);

        int crownX = x;
        for (int row = 0; row < height; row++)
        {
            // The trunk bends more the higher it gets.
            float t = row / (float)height;
            crownX = x + (int)MathF.Round(lean * height / 4f * t * t);
            var colour = row % 3 == 2 ? ring : bark;
            c.Put(crownX, y - row, colour);
            c.Put(crownX + 1, y - row, colour);
        }

        int crownY = y - height;
        (int Length, float Rise, float Droop)[] fronds = { (10, 1.0f, 0.12f), (9, 0.4f, 0.10f), (6, -0.3f, 0.10f) };
        foreach (int side in new[] { -1, 1 })
        {
            foreach (var (length, rise, droop) in fronds)
            {
                for (int i = 0; i <= length; i++)
                {
                    int fx = crownX + (side > 0 ? 1 : 0) + side * i;
                    int fy = crownY + (int)MathF.Round(-rise * i + droop * i * i);
                    c.Put(fx, fy + 1, under);
                    c.Put(fx, fy, frond);
                }
            }
        }

        var coconut = Color.FromArgb(104, 70, 44);
        c.Put(crownX, crownY + 2, coconut);
        c.Put(crownX + 1, crownY + 2, coconut);
    }

    // ---- Small things on the ground -------------------------------------------------------

    private static void Pebble(Canvas c, int x, int y)
    {
        c.Put(x, y, Stone);
        c.Put(x + 1, y, Stone);
        c.Put(x + 2, y, StoneDark);
        c.Put(x + 1, y - 1, StoneLight);
    }

    private static void Starfish(Canvas c, Random rng, int x, int y)
    {
        var colour = rng.Next(2) == 0 ? Color.FromArgb(244, 132, 80) : Color.FromArgb(240, 150, 170);
        c.Put(x, y, colour);
        c.Put(x - 1, y, colour);
        c.Put(x + 1, y, colour);
        c.Put(x, y - 1, colour);
        c.Put(x, y + 1, colour);
    }

    private static void Shell(Canvas c, int x, int y)
    {
        c.Put(x, y, Color.FromArgb(250, 240, 232));
        c.Put(x + 1, y, Color.FromArgb(240, 190, 190));
        c.Put(x, y - 1, Color.FromArgb(240, 190, 190));
    }

    private static void Crab(Canvas c, int x, int y) => c.Stamp(x, y, false, new[]
    {
        "r...r",
        ".rrr.",
        "rkrkr",
        "r.r.r",
    }, ('r', Color.FromArgb(226, 84, 60)), ('k', Color.FromArgb(40, 30, 30)));

    // ---- Landmarks ------------------------------------------------------------------------

    private static void Smoke(Canvas c, int x, int y)
    {
        for (int i = 0; i < 9; i++)
        {
            var puff = Color.FromArgb(210 - i * 20, 236, 236, 240);
            int sx = x + (int)MathF.Round(MathF.Sin(i * 0.8f) * 1.5f) + i / 3;
            c.Put(sx, y - i, puff);
            if (i > 2) c.Put(sx + 1, y - i, puff);
        }
    }

    private static void Cottage(Canvas c, Random rng, int x, int y)
    {
        bool flip = rng.Next(2) == 0;
        var roof = rng.Next(2) == 0 ? Color.FromArgb(196, 84, 70) : Color.FromArgb(92, 116, 170);
        c.Stamp(x, y + 1, flip, new[]
        {
            ".......cc....",
            "....rrrcc....",
            "...rrrrrrr...",
            "..rrRRRRRrr..",
            ".rrRRRRRRRrr.",
            "rrrrrrrrrrrrr",
            ".wwwwwwwwwww.",
            ".wwbbwwwddww.",
            ".wwbbwwwddww.",
            ".wwwwwwwddww.",
            ".sssssssddss.",
        }, ('r', roof), ('R', ControlPaint.Light(roof, 0.3f)), ('w', Color.FromArgb(240, 228, 202)), ('s', Color.FromArgb(212, 198, 172)),
            ('b', Color.FromArgb(130, 190, 226)), ('d', Color.FromArgb(120, 80, 50)), ('c', Color.FromArgb(132, 122, 122)));
        Smoke(c, x + (flip ? -1 : 1), y - 11);
    }

    private static void Cabin(Canvas c, Random rng, int x, int y)
    {
        bool flip = rng.Next(2) == 0;
        c.Stamp(x, y + 1, flip, new[]
        {
            ".........cc..",
            "...rrrrrrcc..",
            "..rrrrrrrrrr.",
            ".rrrrrrrrrrrr",
            "rrrrrrrrrrrrr",
            ".lLlLlLlLlLl.",
            ".llyyllllddl.",
            ".lLyyLlLlddL.",
            ".llllllllddl.",
        }, ('r', Color.FromArgb(84, 62, 58)), ('l', Color.FromArgb(150, 104, 66)), ('L', Color.FromArgb(124, 84, 54)),
            ('y', Color.FromArgb(252, 216, 110)), ('d', Color.FromArgb(86, 56, 40)), ('c', Color.FromArgb(120, 114, 118)));
        Smoke(c, x + (flip ? -4 : 4), y - 9);
    }

    private static void Windmill(Canvas c, Random rng, int x, int y)
    {
        Color wall = Color.FromArgb(240, 232, 214), shade = Color.FromArgb(210, 200, 184);
        Color cap = Color.FromArgb(150, 84, 66), sail = Color.FromArgb(124, 88, 60);
        const int tower = 13;
        for (int row = 0; row < tower; row++)
        {
            // The tower tapers towards the cap.
            int half = 2 + (tower - row) / 5;
            for (int dx = -half; dx <= half; dx++) c.Put(x + dx, y + 1 - tower + row, dx > 0 ? shade : wall);
        }

        c.Put(x, y, cap);
        c.Put(x, y - 1, cap);
        for (int dx = -2; dx <= 2; dx++) c.Put(x + dx, y - tower, cap);
        for (int dx = -1; dx <= 1; dx++) c.Put(x + dx, y - tower - 1, cap);

        // Four sails, caught at a different angle on every mill.
        int hubY = y - tower + 1;
        bool upright = rng.Next(2) == 0;
        for (int i = 1; i <= 8; i++)
        {
            foreach (var (dx, dy) in upright ? new[] { (0, -1), (1, 0), (0, 1), (-1, 0) } : new[] { (1, -1), (1, 1), (-1, 1), (-1, -1) })
            {
                c.Put(x + dx * i, hubY + dy * i, sail);
                // The cloth hangs off one side of each spar.
                if (i > 2) c.Put(x + dx * i - dy, hubY + dy * i + dx, Color.White);
            }
        }

        c.Put(x, hubY, cap);
    }

    /// <summary>A stretch of still water set into the slope, with reeds at one end.</summary>
    private static void Pond(Canvas c, Random rng, int x, int y) => Pool(c, rng, x, y + 4, rng.Next(9, 15));

    private static void Lake(Canvas c, Random rng, int x, int y) => Pool(c, rng, x, y + 5, rng.Next(16, 28));

    private static void Pool(Canvas c, Random rng, int x, int y, int reach)
    {
        c.Blob(x, y, reach, 2.4f, Water, Water, Water);
        for (int dx = -reach + 2; dx <= reach - 2; dx++) c.Put(x + dx, y - 2, WaterLight);
        for (int i = 0; i < reach / 3; i++)
        {
            int gx = x + rng.Next(-reach + 3, reach - 4);
            c.Put(gx, y + rng.Next(0, 2), WaterLight);
            c.Put(gx + 1, y, WaterLight);
        }

        Color reed = Color.FromArgb(60, 122, 62), head = Color.FromArgb(124, 84, 54);
        int side = rng.Next(2) == 0 ? -1 : 1;
        for (int i = 0; i < 3; i++)
        {
            int rx = x + side * (reach - 1 - i * 2), tall = 3 + (i * 2 + reach) % 3;
            for (int k = 0; k < tall; k++) c.Put(rx, y - 1 - k, reed);
            c.Put(rx, y - 1 - tall, head);
        }
    }

    private static void Haystacks(Canvas c, Random rng, int x, int y)
    {
        Color hay = Color.FromArgb(232, 196, 92), lit = Color.FromArgb(248, 222, 130), dark = Color.FromArgb(196, 156, 66);
        for (int i = rng.Next(2, 4); i > 0; i--)
        {
            int hx = x + (i - 2) * rng.Next(8, 11);
            c.Blob(hx, y + 1, 3.2f, 4.2f, hay, lit, dark);
        }
    }

    /// <summary>A few fruit trees planted close together.</summary>
    private static void Orchard(Canvas c, Random rng, int x, int y)
    {
        var apple = Color.FromArgb(226, 60, 60);
        for (int i = -1; i <= 1; i++)
        {
            int tx = x + i * 11;
            RoundTree(c, rng, tx, y + (i == 0 ? 1 : 0), Leaf);
            for (int k = 0; k < 4; k++) c.Recolour(tx + rng.Next(-5, 7), y - rng.Next(7, 14), apple);
        }
    }

    private static void Grove(Canvas c, Random rng, int x, int y)
    {
        // Tallest in the middle, drawn back to front so the near ones overlap.
        foreach (int dx in new[] { -13, 12, -7, 7, -2, 3 })
        {
            int height = rng.Next(16, 24) - Math.Abs(dx) / 2;
            Pine(c, x + dx, y + 3, height, Color.FromArgb(46, 118, 80), Color.FromArgb(30, 86, 66), BarkDark);
        }
    }

    private static void OldOak(Canvas c, Random rng, int x, int y) => RoundTree(c, rng, x, y, Leaf, size: 2.1f);

    private static void BirchStand(Canvas c, Random rng, int x, int y)
    {
        foreach (int dx in new[] { -9, 8, -3, 3 }) Birch(c, rng, x + dx, y + rng.Next(0, 2));
    }

    private static void Camp(Canvas c, Random rng, int x, int y)
    {
        var canvas = rng.Next(2) == 0 ? Color.FromArgb(236, 146, 62) : Color.FromArgb(222, 84, 80);
        c.Stamp(x, y + 1, false, new[]
        {
            ".....t.....",
            "....tTt....",
            "...ttTtt...",
            "..ttdTdtt..",
            ".tttdddttt.",
            "ttttdddtttt",
        }, ('t', canvas), ('T', ControlPaint.Light(canvas, 0.5f)), ('d', Color.FromArgb(74, 44, 40)));

        // A fire in front of it, on a ring of logs.
        int fx = x + 10;
        c.Put(fx - 1, y + 1, BarkDark);
        c.Put(fx, y + 1, Bark);
        c.Put(fx + 1, y + 1, BarkDark);
        c.Put(fx, y, Color.FromArgb(246, 130, 40));
        c.Put(fx - 1, y, Color.FromArgb(232, 84, 40));
        c.Put(fx + 1, y, Color.FromArgb(232, 84, 40));
        c.Put(fx, y - 1, Color.FromArgb(255, 214, 80));
        c.Put(fx, y - 2, Color.FromArgb(246, 130, 40));
        Smoke(c, fx, y - 4);
    }

    private static void FallenLog(Canvas c, Random rng, int x, int y)
    {
        int length = rng.Next(9, 15);
        for (int dx = 0; dx < length; dx++)
        {
            c.Put(x + dx, y - 2, Color.FromArgb(150, 108, 72));
            c.Put(x + dx, y - 1, Bark);
            c.Put(x + dx, y, BarkDark);
        }

        // The sawn end, and moss along the top.
        c.Put(x, y - 1, Color.FromArgb(222, 190, 140));
        c.Put(x, y - 2, Color.FromArgb(200, 164, 116));
        var moss = Color.FromArgb(104, 176, 84);
        for (int dx = 3; dx < length - 1; dx += rng.Next(1, 4)) c.Put(x + dx, y - 3, moss);
        c.Put(x + length - 3, y - 3, Bark);
        c.Put(x + length - 3, y - 4, Bark);
    }

    private static void Stumps(Canvas c, Random rng, int x, int y)
    {
        for (int i = 0; i < 2; i++)
        {
            int sx = x + i * rng.Next(7, 12), tall = 2 + i;
            for (int dx = 0; dx < 4; dx++)
            {
                for (int k = 0; k < tall; k++) c.Put(sx + dx, y - k, dx == 3 ? BarkDark : Bark);
                c.Put(sx + dx, y - tall, Color.FromArgb(214, 180, 128));
            }
        }

        Mushroom(c, rng, x + 4, y);
    }

    private static void Boulder(Canvas c, Random rng, int x, int y)
    {
        float r = 3.5f + (float)rng.NextDouble() * 2f;
        c.Blob(x, y - r * 0.3f, r * 1.3f, r, Stone, StoneLight, StoneDark);
        c.Blob(x + r * 1.5f, y, r * 0.6f, r * 0.5f, Stone, StoneLight, StoneDark);
        // Moss on the crown.
        var moss = Color.FromArgb(104, 176, 84);
        for (int dx = -1; dx <= 2; dx++) c.Recolour(x + dx, (int)(y - r * 1.3f) + 1, moss);
    }

    private static void Deer(Canvas c, Random rng, int x, int y) => c.Stamp(x, y, rng.Next(2) == 0, new[]
    {
        "n.n.......",
        ".nn.......",
        "bbb.......",
        "kbb.......",
        ".bbbbbbbbw",
        ".bBBBBBBb.",
        ".bbbbbbbb.",
        ".b.b..b.b.",
        ".b.b..b.b.",
        ".d.d..d.d.",
    }, ('b', Color.FromArgb(170, 116, 72)), ('B', Color.FromArgb(190, 136, 88)), ('n', Color.FromArgb(120, 86, 58)),
        ('w', Color.White), ('k', Color.FromArgb(40, 30, 30)), ('d', Color.FromArgb(70, 50, 40)));

    private static void Fence(Canvas c, Random rng, int x, int y)
    {
        Color wood = Color.FromArgb(232, 222, 200), shade = Color.FromArgb(196, 182, 160);
        int length = rng.Next(5, 9) * 5;
        for (int dx = 0; dx <= length; dx++)
        {
            int fx = x - length / 2 + dx;
            if (dx % 5 == 0)
            {
                for (int k = 0; k <= 4; k++) c.Put(fx, y - k, k == 4 ? wood : shade);
            }
            else
            {
                c.Put(fx, y - 3, wood);
                c.Put(fx, y - 1, wood);
            }
        }
    }

    /// <summary>A thick patch of tall flowers in every colour.</summary>
    private static void FlowerBed(Canvas c, Random rng, int x, int y)
    {
        var stem = Color.FromArgb(52, 130, 60);
        for (int dx = -9; dx <= 9; dx++)
        {
            if (rng.Next(4) == 0) continue;
            int tall = rng.Next(2, 6);
            for (int k = 1; k < tall; k++) c.Put(x + dx, y - k, stem);
            c.Put(x + dx, y - tall, Petals[rng.Next(Petals.Length)]);
        }
    }

    private static void Sailboat(Canvas c, Random rng, int x, int y)
    {
        var sail = rng.Next(3) switch
        {
            0 => Color.FromArgb(244, 96, 84),
            1 => Color.FromArgb(252, 214, 96),
            _ => Color.FromArgb(120, 200, 150),
        };
        // Somewhere out on the water rather than on the horizon.
        int waterline = y + rng.Next(2, 9);
        c.Stamp(x, waterline, rng.Next(2) == 0, new[]
        {
            "...m....",
            "..wm....",
            "..wmt...",
            ".wwmt...",
            ".wwmtt..",
            "wwwmttt.",
            "...m....",
            "hhhhhhhh",
            ".HHHHHH.",
        }, ('w', Color.White), ('t', sail), ('m', BarkDark), ('h', Color.FromArgb(250, 246, 236)), ('H', Color.FromArgb(206, 74, 66)));
        for (int dx = -5; dx <= 5; dx += 2) c.Put(x + dx, waterline + 1, WaterLight);
    }

    private static void Island(Canvas c, Random rng, int x, int y)
    {
        Color sand = Color.FromArgb(240, 214, 150), lit = Color.FromArgb(250, 234, 184);
        Palm(c, rng, x + 2, y, rng.Next(7, 11));
        Palm(c, rng, x - 4, y + 1, rng.Next(5, 8));
        c.Blob(x, y + 2, 11, 2.6f, sand, lit, sand);
        Bush(c, rng, x + 5, y, DeepLeaf);
    }

    private static void SeaRocks(Canvas c, Random rng, int x, int y)
    {
        int waterline = y + rng.Next(3, 8);
        foreach (int dx in new[] { -6, 5, 0 })
        {
            float r = 2f + (float)rng.NextDouble() * 2.5f;
            c.Blob(x + dx, waterline - r * 0.6f, r, r * 1.3f, StoneDark, Stone, Color.FromArgb(84, 90, 108));
        }

        // Foam where the swell breaks against them.
        for (int dx = -9; dx <= 8; dx++)
        {
            if (rng.Next(3) > 0) c.Put(x + dx, waterline + 1, Color.FromArgb(232, 246, 252));
        }
    }

    private static void Lighthouse(Canvas c, Random rng, int x, int y)
    {
        // It stands on a rocky point.
        c.Blob(x, y + 1, 9, 4, StoneDark, Stone, Color.FromArgb(84, 90, 108));
        c.Blob(x - 7, y + 2, 4, 2.5f, StoneDark, Stone, Color.FromArgb(84, 90, 108));

        Color white = Color.FromArgb(246, 244, 238), red = Color.FromArgb(214, 70, 62);
        int foot = y - 3;
        const int tower = 17;
        for (int row = 0; row < tower; row++)
        {
            int half = row < 7 ? 2 : 1;
            var band = row / 4 % 2 == 0 ? red : white;
            for (int dx = -half; dx <= half; dx++) c.Put(x + dx, foot - row, dx == half ? ControlPaint.Dark(band, 0.05f) : band);
        }

        // The gallery, the lamp and its cap.
        int top = foot - tower;
        for (int dx = -2; dx <= 2; dx++) c.Put(x + dx, top, Color.FromArgb(60, 60, 70));
        for (int dx = -1; dx <= 1; dx++)
        {
            c.Put(x + dx, top - 1, Color.FromArgb(255, 236, 130));
            c.Put(x + dx, top - 2, Color.FromArgb(255, 236, 130));
            c.Put(x + dx, top - 3, red);
        }

        c.Put(x, top - 4, red);
    }

    private static void Parasol(Canvas c, Random rng, int x, int y)
    {
        var stripe = rng.Next(2) == 0 ? Color.FromArgb(236, 84, 84) : Color.FromArgb(72, 150, 220);
        c.Stamp(x, y + 1, rng.Next(2) == 0, new[]
        {
            "....p.........",
            "..sswwss......",
            ".ssswwsss.....",
            "sssswwssss....",
            "....p.........",
            "....p.........",
            "....p.........",
            "....p.........",
            "....p...tTtTtT",
        }, ('s', stripe), ('w', Color.White), ('p', Color.FromArgb(200, 200, 206)),
            ('t', Color.FromArgb(90, 200, 206)), ('T', Color.White));
    }

    private static void Sandcastle(Canvas c, Random rng, int x, int y) => c.Stamp(x, y + 1, rng.Next(2) == 0, new[]
    {
        "....pr...",
        "....p....",
        "..s.s.s..",
        "..sSSSs..",
        "s.sSSSs.s",
        "sSSSSSSSs",
        "sSSSdSSSs",
    }, ('s', Color.FromArgb(206, 170, 104)), ('S', Color.FromArgb(226, 192, 124)), ('d', Color.FromArgb(150, 116, 70)),
        ('p', BarkDark), ('r', Color.FromArgb(236, 84, 84)));

    private static void Driftwood(Canvas c, Random rng, int x, int y)
    {
        Color wood = Color.FromArgb(176, 150, 124), pale = Color.FromArgb(206, 186, 160);
        int length = rng.Next(8, 13);
        for (int dx = 0; dx < length; dx++)
        {
            c.Put(x + dx, y, wood);
            c.Put(x + dx, y - 1, pale);
        }

        c.Put(x + 3, y - 2, wood);
        c.Put(x + 4, y - 3, wood);
        c.Put(x + length - 3, y - 2, wood);
        Shell(c, x - 3, y + 2);
    }

    private static void BeachBall(Canvas c, Random rng, int x, int y) => c.Stamp(x, y, false, new[]
    {
        ".rw.",
        "rwwb",
        "ywbb",
        ".yb.",
    }, ('r', Color.FromArgb(236, 84, 84)), ('w', Color.White), ('b', Color.FromArgb(72, 150, 220)), ('y', Color.FromArgb(252, 214, 96)));

    private sealed class Canvas
    {
        public Canvas(int width)
        {
            Width = width;
            Pixels = new Color?[width, Height];
        }

        public int Width { get; }

        public Color?[,] Pixels { get; }

        // Row 0 is kept clear so that nothing is ever cut off flat against the top of the strip.
        public void Put(int x, int y, Color colour)
        {
            if (x >= 0 && x < Width && y >= 1 && y < Height) Pixels[x, y] = colour;
        }

        /// <summary>Colours a pixel only where something is already drawn: an apple on a tree, moss on a rock.</summary>
        public void Recolour(int x, int y, Color colour)
        {
            if (x >= 0 && x < Width && y >= 1 && y < Height && Pixels[x, y] is not null) Pixels[x, y] = colour;
        }

        /// <summary>The row of the land's surface at column <paramref name="x"/>.</summary>
        public int Top(Func<int, int> land, int x) => Height - Math.Clamp(land(x), 1, Height - 1);

        /// <summary>Fills everything under a skyline, with a brighter rim along its top.</summary>
        public void Land(Func<int, int> land, Color body, Color rim)
        {
            for (int x = 0; x < Width; x++)
            {
                int top = Top(land, x);
                for (int y = top; y < Height; y++) Put(x, y, y == top ? rim : body);
            }
        }

        /// <summary>A rounded mass, lit from the upper left and shaded underneath.</summary>
        public void Blob(float cx, float cy, float rx, float ry, Color body, Color light, Color dark)
        {
            for (int y = (int)MathF.Floor(cy - ry); y <= (int)MathF.Ceiling(cy + ry); y++)
            {
                for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    if (dx * dx + dy * dy > 1f) continue;
                    Put(x, y, dy > 0.45f ? dark : dx + dy < -0.5f ? light : body);
                }
            }
        }

        /// <summary>
        /// Draws a little picture given as rows of characters, centred on column x with its bottom
        /// row on row y. Each character is looked up in the palette; anything else is left empty.
        /// </summary>
        public void Stamp(int x, int y, bool flip, string[] art, params (char Key, Color Colour)[] palette)
        {
            int width = art[0].Length;
            for (int row = 0; row < art.Length; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    char key = art[row][col];
                    foreach (var (k, colour) in palette)
                    {
                        if (k != key) continue;
                        Put(x - width / 2 + (flip ? width - 1 - col : col), y - art.Length + 1 + row, colour);
                        break;
                    }
                }
            }
        }
    }
}
