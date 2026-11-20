namespace SonkinDesktopPets;

/// <summary>
/// One screen's scenery: a strip along the bottom of its working area, sitting on the taskbar.
/// It always stays behind every other window, the pets included, whichever layer they are on.
/// </summary>
internal sealed class SceneryForm : PetOverlayForm
{
    private readonly Bitmap _picture;
    private readonly Rectangle _strip;

    // The picture never changes, so a slow tick is enough to keep it at the bottom of the stack.
    public SceneryForm(Scene scene, Rectangle workArea, int scale, int seed)
        : base(background: true, interval: 500)
    {
        _picture = Scenery.Build(scene, workArea.Width, scale, seed);
        _strip = new Rectangle(workArea.Left, workArea.Bottom - _picture.Height, workArea.Width, _picture.Height);
        SetArea(workArea);
    }

    protected override void Advance(float dt)
    {
    }

    protected override Rectangle ContentBounds() => _strip;

    protected override void Draw(Graphics g) => g.DrawImageUnscaled(_picture, _strip.Location);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowStack.AddFloor(Handle);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        WindowStack.RemoveFloor(Handle);
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _picture.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>The scenery on every screen the pets are allowed on.</summary>
internal sealed class Backdrop : IDisposable
{
    private readonly List<SceneryForm> _strips = new();

    /// <summary>Replaces the scenery: one strip of <paramref name="scene"/> for each working area.</summary>
    public void Set(Scene scene, Rectangle[] workAreas, int scale)
    {
        Dispose();
        if (scene == Scene.None) return;

        for (int i = 0; i < workAreas.Length; i++)
        {
            // Seeded by the screen, so a screen keeps its landscape from one day to the next.
            var strip = new SceneryForm(scene, workAreas[i], scale, (int)scene * 7919 + i * 104729 + workAreas[i].Width);
            _strips.Add(strip);
            strip.Show();
        }
    }

    public void Dispose()
    {
        foreach (var strip in _strips) strip.Dispose();
        _strips.Clear();
    }
}
