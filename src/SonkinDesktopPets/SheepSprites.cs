using System.Drawing.Imaging;

namespace SonkinDesktopPets;

internal enum SheepPose
{
    Stand,
    Blink,
    StepA,
    StepB,
    GrazeA,
    GrazeB,
    Sit,
    Sleep,
    Leap,
}

internal enum SheepCoat
{
    White,
    Black,
    Golden,
}

/// <summary>The sheep's pixel art, generated in code like the cat's. It faces right; feet on the bottom row.</summary>
internal static class SheepSprites
{
    public const int Width = 26;
    public const int Height = 20;

    public static readonly SheepPose[] WalkCycle = { SheepPose.Stand, SheepPose.StepA, SheepPose.Stand, SheepPose.StepB };

    public static readonly Color Grass = Color.FromArgb(90, 190, 70);

    private static readonly Color Outline = Color.FromArgb(20, 20, 20);
    private static readonly int PoseCount = Enum.GetValues<SheepPose>().Length;

    private sealed record Palette(Color Wool, Color Shade, Color Face, Color Muzzle, Color Leg);

    private static readonly Palette White = new(
        Color.FromArgb(250, 250, 245), Color.FromArgb(205, 208, 222), Color.FromArgb(74, 70, 82),
        Color.FromArgb(120, 112, 126), Color.FromArgb(74, 70, 82));

    private static readonly Palette Black = new(
        Color.FromArgb(66, 62, 74), Color.FromArgb(44, 42, 52), Color.FromArgb(150, 146, 158),
        Color.FromArgb(196, 190, 200), Color.FromArgb(110, 106, 118));

    private static readonly Palette Golden = new(
        Color.FromArgb(255, 214, 64), Color.FromArgb(222, 160, 28), Color.FromArgb(112, 74, 30),
        Color.FromArgb(166, 118, 56), Color.FromArgb(112, 74, 30));

    public static readonly Color Sparkle = Color.FromArgb(255, 252, 200);

    // 'o' outline, 'w' wool, 'f' face, 'm' muzzle, 'e' eye. The ear hangs off the left.
    private static readonly string[] Head =
    {
        "...ooo...",
        "..owwwo..",
        "ooofffoo.",
        "offffeffo",
        "oooffeffo",
        "..offfmmo",
        "..offfmmo",
        "...ooooo.",
    };

    /// <summary>Wool is a clump of overlapping discs: centre x, centre y, radius. The last is the tail.</summary>
    private static readonly (float X, float Y, float R)[] Wool =
    {
        (6f, 10f, 3.5f), (8.5f, 7.5f, 3.2f), (12f, 6.8f, 3.4f), (15.5f, 7.8f, 3.3f), (17f, 10.5f, 3.4f),
        (14f, 12f, 3.5f), (10f, 12.3f, 3.5f), (7f, 12f, 3f), (11f, 10f, 4f), (2.4f, 9.5f, 1.4f),
    };

    public static Size SizeAt(int scale) => new(Width * scale, Height * scale);

    /// <summary>One bitmap per <see cref="SheepPose"/>, indexed by the pose.</summary>
    public static Bitmap[] BuildPoses(int scale, bool facingLeft, SheepCoat coat)
    {
        var palette = coat switch
        {
            SheepCoat.Black => Black,
            SheepCoat.Golden => Golden,
            _ => White,
        };
        var poses = new Bitmap[PoseCount];
        for (int i = 0; i < PoseCount; i++) poses[i] = ToBitmap(Render((SheepPose)i, palette), scale, facingLeft);
        return poses;
    }

    private static Bitmap ToBitmap(Color?[,] pixels, int scale, bool mirror)
    {
        var bitmap = new Bitmap(Width * scale, Height * scale, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (pixels[x, y] is not { } colour) continue;
                using var brush = new SolidBrush(colour);
                int drawX = mirror ? Width - 1 - x : x;
                g.FillRectangle(brush, drawX * scale, y * scale, scale, scale);
            }
        }

        return bitmap;
    }

    private static Color?[,] Render(SheepPose pose, Palette palette)
    {
        var px = new Color?[Width, Height];

        void Put(int x, int y, Color colour)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height) px[x, y] = colour;
        }

        bool lying = pose is SheepPose.Sit or SheepPose.Sleep;
        bool grazing = pose is SheepPose.GrazeA or SheepPose.GrazeB;
        int bodyDy = lying ? 3 : 0;

        // Legs first, so the wool covers their tops.
        if (pose == SheepPose.Leap)
        {
            // Flung out fore and aft.
            for (int k = 0; k < 5; k++)
            {
                foreach (int x in new[] { 5 - k, 8 - k, 14 + k, 17 + k })
                {
                    Put(x, 13 + k, k == 4 ? Outline : palette.Leg);
                    Put(x + 1, 13 + k, k == 4 ? Outline : palette.Leg);
                }
            }
        }
        else if (lying)
        {
            // Tucked under, with just the front hooves poking out.
            for (int x = 15; x <= 19; x++) Put(x, Height - 1, Outline);
        }
        else
        {
            int[] legs = { 5, 8, 14, 17 };
            for (int i = 0; i < legs.Length; i++)
            {
                // Diagonal pairs step together, as a walking sheep's do.
                bool stepping = (pose == SheepPose.StepA && i is 0 or 2) || (pose == SheepPose.StepB && i is 1 or 3);
                int x = legs[i] + (stepping ? 1 : 0);
                int foot = Height - 1 - (stepping ? 1 : 0);
                for (int y = 12; y <= foot; y++)
                {
                    Put(x, y, y == foot ? Outline : palette.Leg);
                    Put(x + 1, y, y == foot ? Outline : palette.Leg);
                }
            }
        }

        var wool = new bool[Width, Height];
        foreach (var (cx, cy, r) in Wool)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float dx = x - cx, dy = y - (cy + bodyDy);
                    if (dx * dx + dy * dy <= r * r) wool[x, y] = true;
                }
            }
        }

        bool IsWool(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height && wool[x, y];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (wool[x, y])
                {
                    // A shadow along the underside gives the fleece some roundness.
                    Put(x, y, IsWool(x, y + 1) && IsWool(x, y + 2) ? palette.Wool : palette.Shade);
                }
                else if (IsWool(x - 1, y) || IsWool(x + 1, y) || IsWool(x, y - 1) || IsWool(x, y + 1))
                {
                    Put(x, y, Outline);
                }
            }
        }

        int headX = 16, headY = 2 + bodyDy;
        if (grazing) (headX, headY) = (17, pose == SheepPose.GrazeA ? 11 : 12);
        bool eyesShut = pose is SheepPose.Blink or SheepPose.Sleep;
        for (int row = 0; row < Head.Length; row++)
        {
            for (int col = 0; col < Head[row].Length; col++)
            {
                Color? colour = Head[row][col] switch
                {
                    'o' => Outline,
                    'w' => palette.Wool,
                    'f' => palette.Face,
                    'm' => palette.Muzzle,
                    // A shut eye is just its lower lid.
                    'e' => !eyesShut ? Color.White : Head[row - 1][col] == 'e' ? Outline : palette.Face,
                    _ => null,
                };
                if (colour is { } c) Put(headX + col, headY + row, c);
            }
        }

        return px;
    }
}
