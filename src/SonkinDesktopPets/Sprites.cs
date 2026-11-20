using System.Drawing.Imaging;

namespace SonkinDesktopPets;

/// <summary>Hand-drawn pixel art, generated in code so the app ships no image files.</summary>
internal static class Sprites
{
    public const int Width = 34;
    public const int Height = 22;
    public const int FrameCount = 6;

    /// <summary>Sprite row of the middle of the pastry body, where the rainbow attaches.</summary>
    public const float BodyCentreRow = 9.5f;

    public static readonly Color[] Rainbow =
    {
        Color.FromArgb(255, 0, 0),
        Color.FromArgb(255, 153, 0),
        Color.FromArgb(255, 255, 0),
        Color.FromArgb(51, 255, 0),
        Color.FromArgb(0, 153, 255),
        Color.FromArgb(102, 51, 255),
    };

    public static readonly Color[] StarTints =
    {
        Color.White,
        Color.FromArgb(255, 240, 120),
        Color.FromArgb(255, 170, 230),
        Color.FromArgb(150, 230, 255),
    };

    private static readonly Color Outline = Color.FromArgb(20, 20, 20);
    private static readonly Color Fur = Color.FromArgb(153, 153, 153);
    private static readonly Color Cheek = Color.FromArgb(255, 153, 153);
    private static readonly Color Crust = Color.FromArgb(255, 204, 153);
    private static readonly Color Frosting = Color.FromArgb(255, 153, 255);
    private static readonly Color Sprinkle = Color.FromArgb(255, 51, 153);

    public static readonly Color[] CrumbTints = { Frosting, Sprinkle, Crust };

    private static readonly string[] Head =
    {
        ".oo..........oo.",
        "oggo........oggo",
        "ogggo......ogggo",
        "oggggooooooggggo",
        "oggggggggggggggo",
        "ogggwoggggwogggo",
        "ogggooggogoogggo",
        "ogppggggggggppgo",
        "ogppgoggoggoppgo",
        "oggggooooooogggo",
        ".oggggggggggggo.",
        "..oooooooooooo..",
    };

    private static readonly int[] HeadDx = { 0, 1, 1, 1, 0, 0 };
    private static readonly int[] HeadDy = { 0, 0, 1, 1, 1, 0 };
    private static readonly int[] FeetShift = { 0, 1, 2, 1, 0, -1 };

    private static readonly (int X, int Y)[] Sprinkles =
    {
        (10, 5), (14, 4), (19, 5), (22, 4), (11, 9), (15, 8), (12, 13), (16, 12), (10, 15), (15, 15),
    };

    public static Size SizeAt(int scale) => new(Width * scale, Height * scale);

    public static Bitmap[] BuildFrames(int scale, bool facingLeft)
    {
        var frames = new Bitmap[FrameCount];
        for (int f = 0; f < FrameCount; f++) frames[f] = ToBitmap(RenderFrame(f), scale, facingLeft);
        return frames;
    }

    public static Icon BuildIcon()
    {
        using var bitmap = new Bitmap(Width, Width, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        using (var cat = ToBitmap(RenderFrame(0), 1, false))
        {
            g.DrawImageUnscaled(cat, 0, (Width - Height) / 2);
        }

        return Icon.FromHandle(bitmap.GetHicon());
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

    private static Color?[,] RenderFrame(int frame)
    {
        var px = new Color?[Width, Height];

        void Fill(int x, int y, int w, int h, Color? colour)
        {
            for (int j = y; j < y + h; j++)
            {
                for (int i = x; i < x + w; i++)
                {
                    if (i >= 0 && i < Width && j >= 0 && j < Height) px[i, j] = colour;
                }
            }
        }

        // Tail: a wave that travels along it as the frames advance.
        for (int x = 0; x <= 6; x++)
        {
            int y = 10 + (int)MathF.Round(MathF.Sin(x * 0.9f + frame * MathF.PI / 3f) * 1.5f);
            Fill(x, y - 1, 1, 4, Outline);
            if (x > 0) Fill(x, y, 1, 2, Fur);
        }

        // Feet, drawn first so the pastry covers their tops.
        foreach (int baseX in new[] { 7, 12, 19, 24 })
        {
            int x = baseX + FeetShift[frame];
            Fill(x - 1, 17, 5, 4, Outline);
            Fill(x, 17, 3, 3, Fur);
        }

        // Pastry body.
        Fill(6, 1, 21, 18, Outline);
        foreach (var (x, y) in new[] { (6, 1), (26, 1), (6, 18), (26, 18) }) Fill(x, y, 1, 1, null);
        Fill(7, 2, 19, 16, Crust);
        Fill(8, 3, 17, 14, Frosting);
        foreach (var (x, y) in new[] { (8, 3), (24, 3), (8, 16), (24, 16) }) Fill(x, y, 1, 1, Crust);
        foreach (var (x, y) in Sprinkles) Fill(x, y, 1, 1, Sprinkle);

        // Head.
        int headX = 17 + HeadDx[frame], headY = 6 + HeadDy[frame];
        for (int row = 0; row < Head.Length; row++)
        {
            for (int col = 0; col < Head[row].Length; col++)
            {
                Color? colour = Head[row][col] switch
                {
                    'o' => Outline,
                    'g' => Fur,
                    'w' => Color.White,
                    'p' => Cheek,
                    _ => null,
                };
                if (colour is not null) Fill(headX + col, headY + row, 1, 1, colour);
            }
        }

        return px;
    }

    /// <summary>Pixel offsets (in sprite pixels) lit in each frame of a star's twinkle.</summary>
    public static readonly (int X, int Y)[][] StarFrames =
    {
        new[] { (0, 0) },
        new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) },
        new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (-2, 0), (0, 2), (0, -2) },
        new[] { (0, 0), (2, 0), (-2, 0), (0, 2), (0, -2), (3, 0), (-3, 0), (0, 3), (0, -3) },
        new[] { (3, 0), (-3, 0), (0, 3), (0, -3), (2, 2), (-2, 2), (2, -2), (-2, -2) },
        new[] { (3, 0), (-3, 0), (0, 3), (0, -3) },
    };
}
