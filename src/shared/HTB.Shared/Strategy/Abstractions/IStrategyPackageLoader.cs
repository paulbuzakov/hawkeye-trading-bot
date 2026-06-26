using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Abstractions;

/// <summary>
/// Reads a <em>strategy package</em> — a zip archive carrying <c>meta.json</c> + <c>rules.json</c>
/// at its root — and produces a validated, hash-checked <see cref="StrategyDefinition"/>.
/// Owns only the archive concerns (entry lookup, untrusted-zip limits, UTF-8 decoding) and delegates
/// all JSON validation and hashing to <see cref="IStrategyLoader"/>. Every failure — malformed zip,
/// missing/duplicate entry, oversized entry, invalid encoding — surfaces as a typed configuration
/// exception, the same taxonomy the string-pair loader throws.
/// </summary>
public interface IStrategyPackageLoader
{
    /// <summary>
    /// Loads a strategy from an open, readable, seekable archive <paramref name="package"/> stream.
    /// The caller owns the stream's lifetime (it is left open). This is the single testable seam;
    /// file-path and byte-array intake are caller conveniences built on top of it.
    /// </summary>
    StrategyDefinition Load(Stream package);

    /// <summary>
    /// Non-throwing check step: returns whether the package would load (and the loaded definition when
    /// it does), instead of throwing on a bad archive or a validation failure. For governance/registry
    /// admission. A null stream is still a programming error and throws <see cref="ArgumentNullException"/>.
    /// </summary>
    StrategyValidationResult Validate(Stream package);

    /// <summary>
    /// Safely extracts the package's exact <c>meta.json</c> + <c>rules.json</c> text (applying the
    /// same archive limits as <see cref="Load(Stream)"/>) without validating their content, so a
    /// caller can persist the original bytes. Throws <see cref="StrategyConfigException"/> on a bad
    /// archive; a null stream throws <see cref="ArgumentNullException"/>.
    /// </summary>
    StrategyPackageDocuments ReadDocuments(Stream package);
}
