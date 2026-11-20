# SonkinDesktopPets

A desktop pet for Windows: a pixel-art pop-tart cat that flies around your screen trailing a rainbow and getting up to mischief. Inspired by Desktop Goose.

The cat lives in a transparent, click-through window, so it never gets in the way of what you are clicking on.

## What it does

- **Flies with a rainbow** between random spots, with twinkling stars in its wake.
- **Dashes** off one edge of the screen and streaks back in from the other.
- **Loop-the-loops** and **sine waves**, drawing rainbow circles and squiggles.
- **Steals your cursor**: chases the mouse, grabs it and flies off with it for a couple of seconds.
- **Orbits your cursor** without grabbing it.
- **Delivers notes**: tows a little note window in from off screen and leaves it on your desktop. After a random 5 to 90 seconds it comes back and carries the note away again.
- **Sings**, with a speech bubble and a ring of stars.
- **Sheds pastry crumbs** that fall down the screen.
- **Bolts** when you touch it with the cursor.

There is no sound.

## Running it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Windows.

```
dotnet run --project src/SonkinDesktopPets -c Release
```

The cat appears straight away and an icon is added to the system tray. Only one copy runs at a time.

## Options

Right-click the tray icon:

| Option | What it does |
| --- | --- |
| Screen | Let the cat roam all screens, or confine it to one display |
| Size | Tiny, Small, Medium or Large |
| Stay behind windows | Fly behind every other window, just above the wallpaper, instead of on top. Notes follow the same layer |
| Steal my cursor | Turn cursor stealing on or off |
| Deliver notes | Turn note deliveries on or off |
| Nap (pause) | Hide the cat until you wake it |
| Exit | Quit |

Choices are saved to `%AppData%\SonkinDesktopPets\settings.json`.

### Limiting the cat to one screen

Pick a display under **Screen** in the tray menu, or start it with:

```
SonkinDesktopPets.exe --screen 2
SonkinDesktopPets.exe --screen all
```

The number is the display number Windows shows in its display settings. The choice is remembered for next time. When confined, the cat is clipped to that screen, only goes for the cursor while it is on that screen, and never drags the cursor or drops a note outside it.

## Building a standalone exe

This produces a single `publish\SonkinDesktopPets.exe` (about 49 MB) that runs on 64-bit Windows without .NET installed:

```
dotnet publish src/SonkinDesktopPets -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

Exit the cat first if it is running from that folder, or the file will be locked. The exe is not code-signed, so Windows SmartScreen may warn the first time it is run on another PC.

### Starting with Windows

Press `Win+R`, run `shell:startup`, and put a shortcut to the exe in the folder that opens.

## Project layout

| Path | Contents |
| --- | --- |
| `src/SonkinDesktopPets/NyanBrain.cs` | The cat's behaviour. Pure logic in screen coordinates, with no windows or real mouse involved |
| `src/SonkinDesktopPets/OverlayForm.cs` | The overlay window and all drawing |
| `src/SonkinDesktopPets/Sprites.cs` | The pixel art, generated in code; the app ships no image files |
| `src/SonkinDesktopPets/NoteForm.cs` | The note windows and their messages |
| `src/SonkinDesktopPets/NyanContext.cs` | Tray menu, settings, and the glue between the brain and the desktop |
| `tests/SonkinDesktopPets.Tests` | Behaviour tests for the brain |

Run the tests with:

```
dotnet test
```

## Licence

The code is released into the public domain under [The Unlicense](LICENSE): do whatever you like with it, no attribution needed.

## Credits

Forked from [desktopNyan](https://github.com/SondreKindem/DesktopNyan).

A fan project, not affiliated with or endorsed by the creators of Nyan Cat or Desktop Goose. The licence above covers this project's code; it does not grant any rights to the Nyan Cat character itself.
