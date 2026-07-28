# Contributing to Serpent's Eyes

Thanks for taking a look. Bug reports, save-format findings, and pull requests are all welcome.

## Before you invest time

This is a finished side project, not something I work on weekly, and **I don't check GitHub
often**. A reply will likely take weeks. I'd rather say that up front than have you polish a
pull request and then hear nothing.

So: small, self-contained changes are the ones most likely to get merged without a long
back-and-forth. If you're planning something large, open an issue describing it first — not for
permission, but so you don't build something substantial that turns out to conflict with how the
save format is handled. And if waiting isn't workable for you, fork it. That's a perfectly good
outcome and part of why this is MIT.

## Reporting a bug

[Open an issue](https://github.com/nooikko/serpents-eyes/issues/new/choose). The bug report form
asks for the app version, the game build, and your OS — please fill those in, because save-format
problems are almost always specific to a game version.

**Do not attach a save file you consider private.** A save contains your progression, your
current map and position, and your loadout. If a file is needed to diagnose something, we will
ask, and you are free to say no.

## Getting set up

```
git clone https://github.com/nooikko/serpents-eyes.git
cd serpents-eyes
dotnet build
dotnet test
```

You need the .NET 10 SDK; the version is pinned in `global.json`. Nothing else is required —
you do not need the game installed to build or to run the tests.

## Project layout

| Path | What it is |
| --- | --- |
| `src/SerpentsEyes.Core` | The save format and game data. Zero dependencies, AOT-safe, cross-platform. |
| `src/SerpentsEyes.App` | The Avalonia viewer. Presentation only — no format logic. |
| `tools/SerpentsEyes.Extractor` | Regenerates the game data from an unpacked copy of the game. |
| `tests/SerpentsEyes.Core.Tests` | Tests for Core, including the round-trip guarantee. |

Format knowledge belongs in Core, not in the app. If the app needs to know something about a
save, Core should expose it.

## House rules

- **Warnings are errors** in every project. `dotnet build` failing on a warning is intentional.
- **Public API in Core needs XML docs.** `GenerateDocumentationFile` is on, so a missing
  `<summary>` fails the build.
- Style is enforced by `.editorconfig`. Run `dotnet format` if something looks off.
- Follow the surrounding code. It favours small methods, explicit names, and comments that
  explain *why* rather than restating the code.

## Changing the save format

This is the part that needs the most care, so it has the most rules.

1. **The round-trip guarantee is not negotiable.** `SaveProfile.Parse(bytes).ToBytes()` must
   reproduce its input byte for byte, for every file it accepts. If you cannot re-emit
   something faithfully, preserve the original bytes rather than guessing — see
   `Internal/SourceLayout.cs` for how that works.
2. **Add a fixture.** If you find a save shape the existing fixtures do not cover, add one.
   `tests/SerpentsEyes.Core.Tests/Fixtures/profile_pending_tags.sav` exists precisely because
   the original three fixtures all happened to have an empty pending-tag list, which hid a real
   bug for a long time.
3. **Prefer a synthesized file over a real one** where you can. `SaveFileBuilder` constructs
   saves byte by byte, which is how the edge cases real saves never exhibit get tested.
4. **Never throw anything but `SaveFormatException`** out of the parser. It is fed arbitrary
   files off disk; `MalformedInputTests` asserts this and fuzzes for it.
5. **Be honest about what you know.** If a field's meaning is unconfirmed, name it accordingly
   and say so in the doc comment. `RunSnapshot.PendingTags` documents both what was observed
   and what remains unknown.

## Regenerating game data

`TagDatabase.g.cs` and everything under `src/SerpentsEyes.App/Assets/` are generated. Do not
edit them by hand — change the extractor and re-run it. See the README for the full workflow,
including how to unpack the game first.

## Pull requests

- Branch from `main`.
- One logical change per PR.
- Add tests for behaviour changes; CI runs on Windows and Linux.
- Explain *why* in the description. The what is visible in the diff.

By contributing, you agree that your contributions are licensed under the
[MIT License](../LICENSE).
