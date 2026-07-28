## What and why

<!-- What changes, and what problem it solves. The diff shows the what; explain the why. -->

## How it was verified

<!-- Tests added, and anything checked by hand. -->

- [ ] `dotnet build` and `dotnet test` pass
- [ ] Tests added or updated for the behaviour change

## If this touches the save format

<!-- Delete this section if it does not. -->

- [ ] The round-trip stays byte-perfect for every file the parser accepts
- [ ] A fixture or `SaveFileBuilder` case covers the new shape
- [ ] The parser still throws nothing but `SaveFormatException`
- [ ] The format table in the README is updated

## If this touches generated files

- [ ] `TagDatabase.g.cs` and `Assets/` were regenerated with the extractor, not hand-edited
