namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// The exact source documents extracted from a strategy package: the raw <c>meta.json</c> and
/// <c>rules.json</c> text, byte-faithful so <c>sha256(RulesJson)</c> still equals the manifest's
/// declared <c>rules-hash</c>. Surfaced so a caller (e.g. the registry writer) can persist the
/// original bytes alongside the loaded definition.
/// </summary>
public sealed record StrategyPackageDocuments(string ManifestJson, string RulesJson);
