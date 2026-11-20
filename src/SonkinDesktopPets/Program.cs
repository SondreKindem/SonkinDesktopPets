using System.Drawing.Imaging;

namespace SonkinDesktopPets;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Developer aid: SonkinDesktopPets --dump-sprites out.png writes the animation frames to a file.
        int dump = Array.IndexOf(args, "--dump-sprites");
        if (dump >= 0 && dump + 1 < args.Length)
        {
            DumpSprites(args[dump + 1]);
            return;
        }

        using var mutex = new Mutex(true, "SonkinDesktopPets.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance) return;

        var settings = Settings.Load();
        ApplyScreenArgument(args, settings);

        using var context = new NyanContext(settings);
        Application.Run(context);
    }

    /// <summary>--screen 2 confines the cat to Display 2; --screen all lets it roam everywhere.</summary>
    private static void ApplyScreenArgument(string[] args, Settings settings)
    {
        int index = Array.IndexOf(args, "--screen");
        if (index < 0 || index + 1 >= args.Length) return;

        string value = args[index + 1];
        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            settings.ScreenDevice = null;
        }
        else if (Screen.AllScreens.FirstOrDefault(s => NyanContext.ScreenNumber(s) == value) is { } screen)
        {
            settings.ScreenDevice = screen.DeviceName;
        }
        else
        {
            string available = string.Join(", ", Screen.AllScreens.Select(NyanContext.ScreenNumber).Order());
            MessageBox.Show($"There is no display \"{value}\". Available: {available}, or \"all\".", "SonkinDesktopPets");
            return;
        }

        settings.Save();
    }

    private static void DumpSprites(string path)
    {
        const int scale = 8;
        var right = Sprites.BuildFrames(scale, facingLeft: false);
        var left = Sprites.BuildFrames(scale, facingLeft: true);
        var cell = Sprites.SizeAt(scale);
        using var sheet = new Bitmap(cell.Width * 3, cell.Height * 4, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(0, 51, 102));
            for (int i = 0; i < Sprites.FrameCount; i++)
            {
                g.DrawImageUnscaled(right[i], i % 3 * cell.Width, i / 3 * cell.Height);
                g.DrawImageUnscaled(left[i], i % 3 * cell.Width, (2 + i / 3) * cell.Height);
            }
        }

        sheet.Save(path, ImageFormat.Png);
    }
}
