# Security Policy

## Scope

Serpent's Eyes is an offline desktop application. It has no network access, no telemetry, no
server component, and no credentials to steal. It does one thing that carries any risk at all:
it parses untrusted binary files off disk.

That makes the realistic threat a **malicious `.sav` file** — something crafted to crash the
app, hang it, exhaust memory, or drive the parser into reading or writing out of bounds when
someone is handed a save file by a stranger.

The parser is written defensively against this: lengths and counts are bounded before any
allocation, the recursive-descent formula parsers are depth-limited, and `MalformedInputTests`
fuzzes truncated input, random input, and corruption below an intact header, asserting that
nothing but `SaveFormatException` ever escapes the parser and that whatever does parse still
round-trips and survives the readers built on top of it.
If you find a way around that, it is a security issue and I would like to know.

Also in scope: anything in the release pipeline that could ship a build that does not match this
source.

## Supported versions

The latest release. This is a small project; fixes go forward, not into old tags.

## Reporting a vulnerability

Please report privately through
[GitHub Security Advisories](https://github.com/nooikko/serpents-eyes/security/advisories/new)
rather than opening a public issue.

Include what you did, what happened, and the affected version. A crafted file that demonstrates
the problem is the most useful thing you can attach — and unlike a bug report, a synthesized
file is preferred over a real save, since it will not contain anyone's progression.

On timing: this is a side project and I check GitHub about once a week, so please allow a few
days for an initial response rather than expecting a same-day reply. A security advisory emails
me, which is the most reliable way to reach me.

Once I've seen it, I'll confirm the report, agree a fix, and credit you in the release notes
unless you'd rather I didn't.

## Out of scope

- The game itself, and anything about how it stores or validates its own saves.
- Editing your own save files. `SaveProfile.Save` is deliberately write-capable; using it to
  give yourself things is the intended use of a library, not a vulnerability.
- Losing data by overwriting a save you did not back up. The app never writes; the library will
  if you ask it to.
