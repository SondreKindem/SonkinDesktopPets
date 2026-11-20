# SonkinDesktopPets

Desktop pets for Windows: a pixel-art pop-tart cat that flies around your screen trailing a rainbow and getting up to mischief, and a flock of sheep that graze along the taskbar and climb on your windows. Inspired by Desktop Goose.

The pets live in transparent, click-through windows, so they never get in the way of what you are clicking on.

## What the cat does

- **Flies with a rainbow** between random spots, with twinkling stars in its wake.
- **Dashes** off one edge of the screen and streaks back in from the other.
- **Loop-the-loops** and **sine waves**, drawing rainbow circles and squiggles.
- **Steals your cursor**: chases the mouse, grabs it and flies off with it for a couple of seconds.
- **Orbits your cursor** without grabbing it.
- **Delivers notes**: tows a little note window in from off screen and leaves it on your desktop. After a random 5 to 90 seconds it comes back and carries the note away again.
- **Sings**, with a speech bubble and a ring of stars.
- **Sheds pastry crumbs** that fall down the screen.
- **Bolts** when you touch it with the cursor.

## What the sheep do

Sheep walk rather than fly. They stand on the bottom of the screen, on top of the taskbar, and on the top edges of windows that are not maximized.

- **Drop in** from above the screen when they arrive, landing on whatever is below.
- **Graze** along the bottom of the screen, nibbling a tuft of grass.
- **Wander** back and forth along whatever they are standing on.
- **Leap** up onto windows, across to other windows and back down again.
- **Sit down** on a window to enjoy the view, and sometimes **doze off**.
- **Ride along** when you drag the window they are on, and **tumble off** when it is closed, minimized, maximized or covered by another window.
- **Bleat**, with a speech bubble.
- **Pronk**: a string of bouncy hops, as lambs do.
- **Get the zoomies** and tear back and forth.
- **Jump in fright** and run away when you poke them with the cursor, which also wakes a sleeping sheep.
- **Watch the cursor** while standing around.
- Now and then one of the flock is a **black sheep**.
- Very rarely (1 in 25) a new sheep is a sparkling **golden sheep**.

## What they do together

When the cat and the sheep are both out:

- **The cat serenades a sheep**: it flies over, hovers just above and sings, and the sheep bleats back with a heart.
- **The cat carries a sheep off**: it grabs one by the fleece, flies away with it dangling, and lets go. The sheep drops onto whatever is below.
- **Sheep are spooked** when the cat tears past close by in a dash, loop or wave: they jump and run the other way.
- **Sheep watch the cat** when it is near, instead of the cursor.

The cat leaves at least 30 seconds between visits to the flock.

## Scenery

Pick a pixel-art landscape under **Scenery** in the tray menu and it is drawn along the bottom of each screen, sitting on the taskbar: a **meadow**, a **forest**, **mountains** or a **beach**. It is off to begin with.

The scenery always stays behind your windows, just above the wallpaper, so it only shows where the desktop does. Like the pets it is click-through, but it does cover any desktop icons in the bottom rows. Each screen gets its own stretch of landscape, which stays the same from one day to the next.

The pets make use of what is in the landscape:

- Sheep walk over to **drink** from ponds and lakes, **eat** from haystacks, flower beds and the orchard, **lie down in the shade** of big trees and parasols, **sit by the campfire** and **bleat at the deer**.
- The cat flies up to **perch** on tall things: the lighthouse, the windmill, cottage and cabin roofs, the old oak and snowy summits. It sits a while, sometimes sings from up there, and bolts if you poke it. It leaves at least 25 seconds between perches.

There is no sound.

## Running it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Windows.

```
dotnet run --project src/SonkinDesktopPets -c Release
```

The cat and one sheep appear straight away and an icon is added to the system tray. Only one copy runs at a time.

## Options

Right-click the tray icon:

| Option | What it does |
| --- | --- |
| Nyan cat | Show or hide the cat |
| Sheep | Add or remove sheep, up to 12 at a time |
| Screen | Let the pets roam all screens, or confine them to one display |
| Size | Tiny, Small, Medium or Large |
| Scenery | None, Meadow, Forest, Mountains or Beach along the bottom of the screen |
| Stay behind windows | Stay behind every other window, just above the wallpaper, instead of on top. Notes follow the same layer |
| Steal my cursor | Turn the cat's cursor stealing on or off |
| Deliver notes | Turn the cat's note deliveries on or off |
| Nap (pause) | Hide all the pets until you wake them |
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

### Releases

Pushing a version tag makes GitHub Actions run the tests, build the exe and publish a release with it attached:

```
git tag v1.0.0
git push origin v1.0.0
```

A tag with a suffix, such as `v1.1.0-beta.1`, is published as a pre-release. Running the **Release** workflow by hand from the Actions tab builds the exe as a downloadable artifact without making a release.

### Starting with Windows

Press `Win+R`, run `shell:startup`, and put a shortcut to the exe in the folder that opens.

## Project layout

| Path | Contents |
| --- | --- |
| `src/SonkinDesktopPets/NyanBrain.cs` | The cat's behaviour. Pure logic in screen coordinates, with no windows or real mouse involved |
| `src/SonkinDesktopPets/OverlayForm.cs` | The cat's overlay window and its drawing |
| `src/SonkinDesktopPets/Sprites.cs` | The cat's pixel art, generated in code; the app ships no image files |
| `src/SonkinDesktopPets/NoteForm.cs` | The note windows and their messages |
| `src/SonkinDesktopPets/SheepBrain.cs` | A sheep's behaviour. Pure logic again: it is told what there is to stand on |
| `src/SonkinDesktopPets/Platforms.cs` | Works out which window edges a sheep can stand on |
| `src/SonkinDesktopPets/DesktopWindows.cs` | Lists the real windows on the desktop |
| `src/SonkinDesktopPets/SheepForm.cs`, `SheepSprites.cs` | A sheep's overlay window, drawing and pixel art |
| `src/SonkinDesktopPets/SheepFlock.cs` | Creates and removes sheep, and shares one window scan between them |
| `src/SonkinDesktopPets/Scenery.cs` | The landscapes, generated in code from a seed |
| `src/SonkinDesktopPets/SceneryForm.cs` | The windows that show a landscape on each screen |
| `src/SonkinDesktopPets/PetOverlayForm.cs` | The click-through, per-pixel-alpha window every pet draws into |
| `src/SonkinDesktopPets/NyanContext.cs` | Tray menu, settings, and the glue between the brains and the desktop |
| `tests/SonkinDesktopPets.Tests` | Behaviour tests for the brains and the platform finder |

Run the tests with:

```
dotnet test
```

## Licence

The code is released into the public domain under [The Unlicense](LICENSE): do whatever you like with it, no attribution needed.

## Credits

Forked from [desktopNyan](https://github.com/SondreKindem/DesktopNyan).

A fan project, not affiliated with or endorsed by the creators of Nyan Cat or Desktop Goose. The licence above covers this project's code; it does not grant any rights to the Nyan Cat character itself.
