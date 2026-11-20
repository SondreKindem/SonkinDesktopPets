namespace SonkinDesktopPets;

/// <summary>A little note the cat leaves on the desktop. The user closes it when they've had enough.</summary>
internal sealed class NoteForm : Form
{
    private static readonly string[] Messages =
    {
        "nyan nyan nyan nyan\r\nnyan nyan nyan nyan\r\nnyan nyan nyan",
        "have you seen\r\nmy other pop-tart?",
        "to do:\r\n- nyan\r\n- nyan\r\n- nyan (important)",
        "this rainbow isn't\r\ngoing to poop itself",
        "your cursor tastes\r\nlike strawberry",
        "day 4,096:\r\nstill nyaning.",
        "I flew all the way\r\nacross your screen\r\nto bring you this.",
        "meow?\r\n\r\nno.\r\n\r\nnyan.",
        "you look like you\r\ncould use a break.\r\n\r\nnyan.",
        "roses are red\r\nviolets are blue\r\nnyan nyan nyan nyan\r\nnyan nyan nyan nyan",
        "shopping list:\r\n- strawberry pop-tarts\r\n- more pop-tarts\r\n- a second rainbow",
        "I am not a cat.\r\nI am not a pastry.\r\nI am both. Deal with it.",
        "fun fact: I have been\r\nflying since 2011\r\nand have not landed once.",
        "that window looked\r\nimportant.\r\n\r\nI flew through it.",
        "meeting notes:\r\n\r\nnyan.",
        "please do not\r\nfeed the cat.\r\n\r\n(feed the cat.)",
        "error 404:\r\npop-tart not found",
        "you have 1 new message:\r\n\r\nnyan",
        "I counted the stars.\r\nThere are nyan of them.",
        "reminder:\r\nsave your work.\r\nthen look at me.",
        "is it Friday yet?\r\n\r\nnyan (no)",
        "my rainbow is\r\n100% organic\r\nand gluten free.",
        "dear diary,\r\ntoday I stole a cursor.\r\nno regrets.",
        "nyan is a state of mind.",
    };

    private static readonly string[] Titles =
    {
        "nyan.txt", "important.txt", "todo.txt", "note to self.txt", "README.nyan", "untitled.txt",
    };

    private bool _background;

    public NoteForm(Random rng, bool background, TimeSpan minLifetime, TimeSpan maxLifetime)
    {
        _background = background;
        ExpiresAt = Environment.TickCount64
            + rng.NextInt64((long)minLifetime.TotalMilliseconds, (long)maxLifetime.TotalMilliseconds + 1);
        Text = Titles[rng.Next(Titles.Length)];
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = !background;
        ClientSize = new Size(230, 130);
        BackColor = Color.FromArgb(255, 250, 200);

        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Font = new Font("Consolas", 10f),
            ForeColor = Color.FromArgb(60, 60, 60),
            Text = Messages[rng.Next(Messages.Length)],
        });
    }

    /// <summary>When the cat should come for the note, on the <see cref="Environment.TickCount64"/> clock.</summary>
    public long ExpiresAt { get; }

    /// <summary>True to sit behind every other window, on the same layer as a background cat.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Background
    {
        get => _background;
        set
        {
            _background = value;
            TopMost = !value;
            if (value && IsHandleCreated) WindowStack.SendToBottom(Handle);
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        // Showing, clicking or dragging a window normally raises it; a background note stays put.
        if (_background && m.Msg == WindowStack.WM_WINDOWPOSCHANGING) WindowStack.ForceBottom(ref m);
        base.WndProc(ref m);
        // Landing on the very bottom puts the note under the scenery; slide that back beneath it.
        if (_background && m.Msg == WindowStack.WM_WINDOWPOSCHANGED) WindowStack.SinkFloor(Handle);
    }
}
