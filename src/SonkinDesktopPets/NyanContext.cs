using Microsoft.Win32;

namespace SonkinDesktopPets;

/// <summary>Owns the overlay, the tray icon and the notes, and wires the brain to the real desktop.</summary>
internal sealed class NyanContext : ApplicationContext
{
    private const int MaxNotes = 3;

    private static readonly (string Label, int Scale)[] Sizes = { ("Tiny", 2), ("Small", 3), ("Medium", 4), ("Large", 6) };

    private readonly Settings _settings;
    private readonly Random _rng = new();
    private readonly NyanBrain _brain;
    private readonly OverlayForm _overlay;
    private readonly NotifyIcon _tray;
    private readonly List<NoteForm> _notes = new();
    private readonly System.Windows.Forms.Timer _noteTimer = new() { Interval = 1000 };
    private NoteForm? _noteBeingCollected;

    /// <summary>
    /// Each note gets a random lifetime between these two; when it runs out the cat comes to
    /// take the note away, so no note stays much longer than the maximum.
    /// </summary>
    private static readonly TimeSpan MinNoteLifetime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxNoteLifetime = TimeSpan.FromSeconds(90);

    public NyanContext(Settings settings)
    {
        _settings = settings;
        var (bounds, regions) = ResolveArea();

        _brain = new NyanBrain(_rng, Sprites.SizeAt(settings.Scale), bounds, regions)
        {
            MischiefEnabled = settings.Mischief,
            NotesEnabled = settings.Notes,
        };
        _brain.NoteDropped += OnNoteDropped;
        _brain.NoteCollected += () => _noteBeingCollected?.Close();
        _noteTimer.Tick += (_, _) => CollectExpiredNotes();
        _noteTimer.Start();

        _overlay = new OverlayForm(_brain, settings.Scale, settings.Background);
        _overlay.SetArea(bounds);
        _overlay.Show();

        var menu = new ContextMenuStrip();
        menu.Opening += (_, e) =>
        {
            PopulateMenu(menu);
            e.Cancel = false;
        };
        _tray = new NotifyIcon
        {
            Icon = Sprites.BuildIcon(),
            Text = "SonkinDesktopPets",
            ContextMenuStrip = menu,
            Visible = true,
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>"Display 2" for \\.\DISPLAY2; the number Windows shows in display settings.</summary>
    public static string ScreenNumber(Screen screen) =>
        new string(screen.DeviceName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());

    private (Rectangle Bounds, Rectangle[] Regions) ResolveArea()
    {
        var chosen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _settings.ScreenDevice);
        return chosen is not null
            ? (chosen.Bounds, new[] { chosen.Bounds })
            : (SystemInformation.VirtualScreen, Screen.AllScreens.Select(s => s.Bounds).ToArray());
    }

    private void ApplyArea()
    {
        var (bounds, regions) = ResolveArea();
        _brain.SetArea(bounds, regions);
        _overlay.SetArea(bounds);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => ApplyArea();

    private void PopulateMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();

        var screens = new ToolStripMenuItem("Screen");
        bool chosenExists = Screen.AllScreens.Any(s => s.DeviceName == _settings.ScreenDevice);
        screens.DropDownItems.Add(new ToolStripMenuItem("All screens", null, (_, _) => ChooseScreen(null))
        {
            Checked = !chosenExists,
        });
        foreach (var screen in Screen.AllScreens.OrderBy(s => s.DeviceName, StringComparer.Ordinal))
        {
            string label = $"Display {ScreenNumber(screen)} ({screen.Bounds.Width}x{screen.Bounds.Height}{(screen.Primary ? ", primary" : "")})";
            string device = screen.DeviceName;
            screens.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) => ChooseScreen(device))
            {
                Checked = device == _settings.ScreenDevice,
            });
        }

        var sizes = new ToolStripMenuItem("Size");
        foreach (var (label, scale) in Sizes)
        {
            sizes.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) => ChooseScale(scale))
            {
                Checked = scale == _settings.Scale,
            });
        }

        menu.Items.Add(screens);
        menu.Items.Add(sizes);
        menu.Items.Add(new ToolStripMenuItem("Stay behind windows", null, (_, _) =>
        {
            _settings.Background = _overlay.Background = !_settings.Background;
            foreach (var note in _notes) note.Background = _settings.Background;
            _settings.Save();
        })
        { Checked = _settings.Background });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Steal my cursor", null, (_, _) =>
        {
            _settings.Mischief = _brain.MischiefEnabled = !_settings.Mischief;
            _settings.Save();
        })
        { Checked = _settings.Mischief });
        menu.Items.Add(new ToolStripMenuItem("Deliver notes", null, (_, _) =>
        {
            _settings.Notes = _brain.NotesEnabled = !_settings.Notes;
            _settings.Save();
        })
        { Checked = _settings.Notes });
        menu.Items.Add(new ToolStripMenuItem("Nap (pause)", null, (_, _) => _overlay.Paused = !_overlay.Paused)
        {
            Checked = _overlay.Paused,
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));
    }

    private void ChooseScreen(string? device)
    {
        _settings.ScreenDevice = device;
        _settings.Save();
        ApplyArea();
    }

    private void ChooseScale(int scale)
    {
        _settings.Scale = scale;
        _settings.Save();
        _overlay.SetScale(scale);
    }

    private void OnNoteDropped(Point dropPoint)
    {
        var note = new NoteForm(_rng, _settings.Background, MinNoteLifetime, MaxNoteLifetime);
        var screen = Rectangle.Intersect(Screen.FromPoint(dropPoint).WorkingArea, _brain.Bounds);
        int x = dropPoint.X - note.Width / 2, y = dropPoint.Y;
        if (!screen.IsEmpty)
        {
            x = Math.Clamp(x, screen.Left, Math.Max(screen.Left, screen.Right - note.Width));
            y = Math.Clamp(y, screen.Top, Math.Max(screen.Top, screen.Bottom - note.Height));
        }

        note.Location = new Point(x, y);
        note.FormClosed += (_, _) =>
        {
            if (note == _noteBeingCollected)
            {
                _noteBeingCollected = null;
                _brain.NoteToCollect = null;
            }

            _notes.Remove(note);
            _brain.NoteSlotAvailable = _notes.Count < MaxNotes;
        };
        _notes.Add(note);
        _brain.NoteSlotAvailable = _notes.Count < MaxNotes;
        note.Show();
    }

    /// <summary>Points the cat at the oldest note past its lifetime, one note at a time.</summary>
    private void CollectExpiredNotes()
    {
        long now = Environment.TickCount64;
        _noteBeingCollected ??= _notes.Where(n => n.ExpiresAt <= now).MinBy(n => n.ExpiresAt);
        if (_noteBeingCollected is not { } note) return;

        // Re-read the position every time: the user may have dragged the note since.
        var handle = new Point(note.Left + note.Width / 2, note.Top);
        if (_brain.Bounds.Contains(handle))
        {
            _brain.NoteToCollect = handle;
        }
        else
        {
            // Dragged off the cat's screen, where it can't be fetched: just tidy it away.
            note.Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _noteTimer.Dispose();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _tray.Visible = false;
            _tray.Dispose();
            foreach (var note in _notes.ToArray()) note.Dispose();
            _overlay.Dispose();
        }

        base.Dispose(disposing);
    }
}
