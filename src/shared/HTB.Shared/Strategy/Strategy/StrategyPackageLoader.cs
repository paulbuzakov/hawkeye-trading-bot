using System.IO.Compression;
using System.Text;
using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Strategy;

/// <summary>
/// Turns an untrusted strategy <em>package</em> (a zip archive with <c>meta.json</c> + <c>rules.json</c>
/// at its root) into a validated <see cref="StrategyDefinition"/> by extracting the two entries safely
/// and delegating to <see cref="IStrategyLoader"/> (Adapter). It adds no schema rules: it owns only the
/// archive concerns — exact-name entry lookup, untrusted-zip limits (zip-bomb, duplicate, traversal),
/// and byte-faithful UTF-8 decoding — so the downstream hash/determinism/look-ahead guarantees hold
/// unchanged. Every archive-level failure surfaces as a typed <see cref="StrategyConfigException"/>.
/// </summary>
public sealed class StrategyPackageLoader(IStrategyLoader inner) : IStrategyPackageLoader
{
    private const string MetaEntryName = "meta.json";
    private const string RulesEntryName = "rules.json";

    // Conservative caps: a manifest is tiny; a condition tree is small. Both bound memory against a
    // zip bomb regardless of a spoofed central-directory length.
    private const int MaxMetaBytes = 64 * 1024;
    private const int MaxRulesBytes = 1024 * 1024;

    // Strict UTF-8 so the text handed downstream is byte-faithful — its SHA-256 must still equal
    // meta.version.rules-hash. A lenient decoder could silently mangle bytes past the hash check.
    private static readonly UTF8Encoding Utf8Strict = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    private readonly IStrategyLoader _inner = inner;

    /// <inheritdoc />
    public StrategyDefinition Load(Stream package)
    {
        var documents = ReadDocuments(package);
        return _inner.Load(documents.ManifestJson, documents.RulesJson);
    }

    /// <inheritdoc />
    public StrategyPackageDocuments ReadDocuments(Stream package)
    {
        ArgumentNullException.ThrowIfNull(package);

        using var archive = OpenArchive(package);

        var metaJson = ReadEntry(archive, MetaEntryName, MaxMetaBytes);
        var rulesJson = ReadEntry(archive, RulesEntryName, MaxRulesBytes);

        return new StrategyPackageDocuments(metaJson, rulesJson);
    }

    /// <inheritdoc />
    public StrategyValidationResult Validate(Stream package)
    {
        ArgumentNullException.ThrowIfNull(package);

        try
        {
            // Reuse the one Load path (archive extraction + inner validation); a verdict can never
            // disagree with an actual load.
            return StrategyValidationResult.Valid(Load(package));
        }
        catch (StrategyConfigException ex)
        {
            return StrategyValidationResult.Invalid(ex.Message);
        }
    }

    private static ZipArchive OpenArchive(Stream package)
    {
        try
        {
            // leaveOpen: the caller owns the stream's lifetime.
            return new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new StrategyConfigException($"package is not a valid zip archive: {ex.Message}");
        }
    }

    private static string ReadEntry(ZipArchive archive, string name, int maxBytes)
    {
        var entry = ResolveUniqueEntry(archive, name);

        using var stream = entry.Open();
        var bytes = ReadCapped(stream, maxBytes, name);

        try
        {
            return Utf8Strict.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new StrategyConfigException($"{name} is not valid UTF-8 text.");
        }
    }

    private static ZipArchiveEntry ResolveUniqueEntry(ZipArchive archive, string name)
    {
        // Match by exact root name via iteration (not GetEntry, which silently returns the first
        // match). Anything path-separated — "../meta.json", "sub/meta.json" — is not "meta.json", so
        // zip-slip is structurally impossible, and duplicates are detected rather than masked.
        ZipArchiveEntry? found = null;
        foreach (var entry in archive.Entries)
        {
            if (!string.Equals(entry.FullName, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (found is not null)
            {
                throw new StrategyConfigException(
                    $"package contains more than one \"{name}\" entry."
                );
            }

            found = entry;
        }

        return found
            ?? throw new StrategyConfigException($"package is missing a \"{name}\" entry.");
    }

    private static byte[] ReadCapped(Stream stream, int maxBytes, string name)
    {
        // Do not trust entry.Length (spoofable header). Read up to maxBytes + 1: if the extra byte
        // arrives, the entry is over the cap, so we reject without buffering the whole bomb.
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var remaining = maxBytes + 1;

        int read;
        while (
            remaining > 0 && (read = stream.Read(chunk, 0, Math.Min(chunk.Length, remaining))) > 0
        )
        {
            buffer.Write(chunk, 0, read);
            remaining -= read;
        }

        if (buffer.Length > maxBytes)
        {
            throw new StrategyConfigException($"{name} exceeds the {maxBytes / 1024} KiB limit.");
        }

        return buffer.ToArray();
    }
}
