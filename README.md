# Serpent's Eyes

A save-file viewer for **Serpent's Gaze**. Reads the game's `.sav` profiles and shows
progression (classes, weapons, blessings, quests, kills…) plus a snapshot of the run
in progress, in a dark desktop UI with custom window chrome.

Built as two pieces so others can build on it:

| Project | What it is |
| --- | --- |
| `src/SerpentsEyes.Core` | The API: a zero-dependency, AOT-safe .NET library that parses and re-serializes save files with a **byte-perfect round-trip guarantee**. |
| `src/SerpentsEyes.App` | A read-only Avalonia desktop viewer built on top of the Core library. |

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
