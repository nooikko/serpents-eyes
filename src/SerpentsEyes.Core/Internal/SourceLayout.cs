namespace SerpentsEyes.Core.Internal;

/// <summary>
/// Where each region of a parsed file lives in the original byte buffer, plus a copy of the
/// values that were read out of it.
/// </summary>
/// <remarks>
/// This is what makes the round-trip guarantee hold. Rather than teaching the serializer to
/// reproduce every encoding the format permits — NUL-only FStrings, absent trailers, non-ASCII
/// single-byte payloads — regions the caller did not touch are copied back verbatim. Only
/// genuinely modified regions go through the canonical encoder, so an unmodified profile is
/// byte-identical by construction rather than by careful bookkeeping.
/// </remarks>
/// <param name="Bytes">The original file contents.</param>
/// <param name="HeaderLength">Length of the header region, i.e. the offset of the record count.</param>
/// <param name="TrailerStart">Offset where the trailer begins; equal to <c>Bytes.Length</c> when there is no trailer.</param>
/// <param name="Header">Snapshot of the header values as parsed.</param>
/// <param name="Trailer">Snapshot of the run-snapshot values as parsed.</param>
internal sealed record SourceLayout(
    byte[] Bytes,
    int HeaderLength,
    int TrailerStart,
    SaveHeader Header,
    RunSnapshot Trailer);
