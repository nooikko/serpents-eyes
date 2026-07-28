# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versioning tracks the public API of `SerpentsEyes.Core`. Changes to the app's UI are a minor
version at most.

## [Unreleased]

## [0.1.0] - 2026-07-28

First public release.

### Added

- Collection browser for Serpent's Gaze saves: Aspects, Weapons and Weapon Masteries, Seeds,
  Blessings, Callings, Relics, Curses, Quests, Shortcuts, Boss Kills, Locations, Emotes, and
  the seven Divinities, with per-category completion counts.
- Locked content shown greyed with unlock hints, so the gallery works as a checklist.
- Counters translated into meaning ("Obtained ×4") instead of raw integers, and scaling
  formulas rendered readably with computed per-level values.
- Current-run panel: map, position, and equipped loadout.
- **Open…** button and drag-and-drop, for saves outside the default directory.
- `SerpentsEyes.Core`: a zero-dependency, AOT-safe library for reading and writing the format,
  plus `TagDatabase`, `TagSemantics`, `UeRichText`, and `ScalingMath`.
- `SaveLocator` discovery across Windows, macOS, native Linux, and Proton prefixes.
- Crash handler that logs to `%LOCALAPPDATA%\SerpentsEyes\crash.log` instead of exiting
  silently.

### Fixed

- **The trailer's leading `int32` is a count of the tag strings that follow it, not an opaque
  field.** It is `0` in any save taken between runs, so treating it as opaque appeared to work;
  when non-zero, the map name, player position, and loadout all shifted by one string. Two of
  three live saves parsed with a progression tag as the map name and an empty loadout.
- `"None"` is Unreal's null `FName`, so it no longer counts as a run in progress.
- Round-trip fidelity: NUL-only and zero-length FStrings collapsing together (−1 byte), an
  empty loadout id overloaded as a truncated-pair sentinel (−5 bytes), a trailer invented for
  files that had none (+4 bytes), and `Encoding.ASCII` replacing every byte ≥ 0x80 with `?`.
  Untouched regions are now copied back verbatim, so an unmodified profile is byte-identical by
  construction.
- Saves can be read while the game holds the file open, which is the normal case for the live
  save directory.
- Malformed input can no longer drive a large allocation from a small file, and the formula
  parsers are depth-limited so nesting cannot overflow the stack.
- Save discovery no longer throws on an unreadable directory, and orders primary profiles ahead
  of autosaves and backups.
- Search covers every category the profile actually has, not a fixed list.

### Changed

- Reading and parsing happen off the UI thread, with a busy indicator; reloading no longer
  parses the file twice.
- Icons ship as WebP downscaled to 384 px: 42.8 MB → 2.4 MB.
- The extractor takes its content root from the command line or `SERPENTS_GAZE_CONTENT`, writes
  to `--out` (default `artifacts/`), and has `--help`. It previously had three absolute paths
  hardcoded to the author's machine.
- Standard window decorations on Linux and macOS instead of the Windows-shaped custom title bar.

[Unreleased]: https://github.com/nooikko/serpents-eyes/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/nooikko/serpents-eyes/releases/tag/v0.1.0
