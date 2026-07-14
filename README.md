# Serpent's Eyes

A save-file viewer for **Serpent's Gaze**. Reads the game's `.sav` profiles and shows
progression (classes, weapons, blessings, quests, kills…) plus a snapshot of the run
in progress, in a dark desktop UI with custom window chrome.

Built as two pieces so others can build on it:

| Project | What it is |
| --- | --- |
| `src/SerpentsEyes.Core` | The API: a zero-dependency, AOT-safe .NET library that parses and re-serializes save files with a **byte-perfect round-trip guarantee**. Includes `GameData.TagDatabase`: in-game display names, descriptions, unlock hints, and god affinities for every progression tag. |
| `src/SerpentsEyes.App` | A read-only Avalonia desktop viewer built on top of the Core library. |
| `tools/SerpentsEyes.Extractor` | Regenerates the tag database from an unpacked copy of the game (see below). |

## Using the app

```
dotnet run --project src/SerpentsEyes.App
```

It auto-detects saves in `%LOCALAPPDATA%\SerpentsGaze\Saved\SaveGames\Steam` and lets
you switch between `profile_0`, the autosave, and backups from the title bar.

Portable single-exe build (no .NET install needed on the target machine):

```
dotnet publish src/SerpentsEyes.App -c Release -r win-x64 -p:PublishAot=true
```

The output is `SerpentsEyes.App.exe` plus three native rendering DLLs (Skia, HarfBuzz,
ANGLE) — copy those four files together; the `.pdb` files are optional debug symbols.

> AOT needs the MSVC linker. If the publish fails with a garbled `vswhere.exe` error
> (a quirk of `findvcvarsall.bat` on some machines), run the publish from a
> **Developer PowerShell for VS 2022** and add `-p:IlcUseEnvironmentalTools=true`.

## Using the Core library

```csharp
using SerpentsEyes.Core;

var profile = SaveProfile.Load(path);              // or SaveProfile.Parse(bytes)

foreach (TagRecord r in profile.Records)           // "Progression.Class.WellRounded" = 1
    Console.WriteLine($"{r.Category}/{r.Name} = {r.Value}");

RunSnapshot run = profile.RunSnapshot;             // map, position, equipped loadout

profile.Find("Progression.Meta.Run.Started")!.Value = 43;   // records are mutable
byte[] bytes = profile.ToBytes();                  // byte-identical if nothing changed
profile.Save(path);                                // write-capable (back up first!)
```

`SaveLocator.DefaultSaveDirectory` / `SaveLocator.FindProfiles()` locate save files on disk.

### Game data (display names)

```csharp
using SerpentsEyes.Core.GameData;

TagDatabase.Find("Progression.Blessing.ChanceHoT")?.DisplayName;   // "Daydreams"
TagDatabase.FindByInternalId("Tree_Warhammer")?.DisplayName;       // "Gatekeeper's Warhammer"
TagDatabase.MapTitle("Majin_HolyCity");                            // "Namah, City of Pilgrims"
```

`TagDatabase.g.cs` is generated — do not edit it. To regenerate after a game update:

```
dotnet run --project tools/SerpentsEyes.Extractor [-- <path-to-Content-root>]
```

The extractor expects an unpacked game at
`C:\Users\elija\Documents\serpents_gaze_workbench\extracted\legacy\NinjaGarden\Content`
by default (see that workbench's CLAUDE.md for how the game was extracted with retoc).
It parses the game's 18 StringTable assets plus ~140 item-definition assets
(classes, weapon trees, blessings, mushrooms/Callings, seeds, utilities/Relics,
curse cards) and emits the generated C# plus a JSON report.

Note the game's own vocabulary, which the app follows: mushrooms are **Callings**,
utilities are **Relics**, items are **Seeds**, and the internal tag names often differ
from display names (tag `Curse.Jester` lives in asset `CA_BardBoy` and displays as
"The Jester"; tag `Curse.HordeCaller` is the "Dreamcallers" curse).

## Save format (`NG_SaveFormat_4`)

Reverse-engineered from real saves; not standard Unreal GVAS. All integers are
little-endian. Strings are FString-style: `int32 length` (including a trailing NUL),
then ASCII bytes and `\0`. A negative length means UTF-16LE with `-length` characters.

```
HEADER
  int32   unknown           observed 522
  int32   unknown           observed 1013
  int16×3 version triplet   observed 5, 5, 4
  byte[4] unknown           observed 52 3C 00 80
  fstring build id          "++NinjaGarden+live"
  byte    unknown           observed 0x03
  fstring format id         "/Script/NinjaGarden.NG_SaveFormat_4"

RECORDS
  int32   count
  count × { fstring tag; int32 value }
          tag   = "Progression.<Category>.<Name>"
          value = counter (1 = unlocked/done once, N = count, 0 = reached but not done)

TRAILER (current-run snapshot)
  int32   unknown           observed 0
  fstring map name          "Majin_HolyCity"
  double  X, Y, Z           player world position
  float   unknown           observed 73.0 (possibly health)
  pairs × { fstring slot type ("Item"); fstring id ("Class_Stronk", …) }
          until a zero/implausible length is read
  zero padding, then FF FF FF FF
```

Unknown byte regions are preserved verbatim by the parser, which is what makes the
round-trip byte-perfect and future save editing safe.

## Development

```
dotnet test          # parser tests run against real save snapshots in tests/…/Fixtures
dotnet build
```

Requires .NET 10. Test fixtures are frozen copies of real saves, so tests stay green
while the game keeps writing to the live save directory.
