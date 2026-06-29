# Relate strategy definitions and rule sets by version

Date: 2026-06-30
Branch: `feat/strategy-rules-domain`
Status: Approved design — ready for implementation plan

## Goal

Make the relationship between a strategy **definition** (`meta.json`) and its
**rule set** (`rules.json`) explicit and enforced at the database level, keyed by
version identity. Today the two tables are related only by convention.

## Current state

| Table | Key | Notes |
| --- | --- | --- |
| `strategy.strategy_definitions` | composite `(id, version)` | maps `StrategyDefinition`; mutable descriptive fields upserted by the loader |
| `strategy.strategy_rule_sets` | single text `version_id` (`{id}@{version}`) | maps `StrategyRuleSetRow`; `rules` jsonb; **no rows are ever written yet** |

There is no foreign key, no navigation, and the two tables store the same version
identity in different shapes. There is also no `rules.json` parser and no
loader-side rule-set persistence — the loader only parses and upserts `meta.json`.

## Scope

**In scope:** the schema/EF-model relationship only.

**Out of scope (deferred):** a `rules.json` parser and loader wiring to persist
definition + rule set together in one transaction. Until that work lands, nothing
writes rows to `strategy_rule_sets`.

## Approach: shared primary key (1:1)

Reshape `strategy_rule_sets` so its primary key is the composite `(id, version)`,
identical to the `strategy_definitions` primary key. The rule set's primary key
*is* its foreign key to the definition — the canonical EF Core 1:1 shared-primary-key
pattern. No redundant identity column; both tables align on the same identity.

Rejected alternatives:

- **Keep `version_id` text PK, FK to a stored `version_id` on definitions.** Requires
  un-ignoring and persisting `StrategyDefinition.VersionId` as a redundant unique
  column plus an alternate key. Stores identity twice (`id` + `version` + `version_id`);
  more columns, more drift risk.
- **Logical/query-only relationship.** Rejected — we want a real DB foreign key.

## What strict 1:1 does and does not guarantee

A relational foreign key enforces **"no rule set without a definition"** (and cascade
on delete). It **cannot** enforce **"every definition has a rule set"** — relational
foreign keys do not constrain the principal side. That direction is satisfied only
when the loader writes both rows in one transaction, which is the deferred
end-to-end task. After this task:

- DB guarantees: every `strategy_rule_sets` row references an existing definition version.
- Not yet guaranteed: that every definition version has a rule set.

## Component changes

### 1. `StrategyRuleSetRow` — `HTB.Strategy.Shared/Persistence/StrategyRuleSetRow.cs`

Replace the single mapped `VersionId` property with the two identity columns that
match the definition key. Keep `VersionId` as a computed (unmapped) convenience and
add the dependent→principal navigation:

```csharp
public StrategyId Id { get; set; }
public StrategyVersion Version { get; set; }
public string Rules { get; set; } = string.Empty;

public StrategyVersionId VersionId => new(Id, Version);     // computed, not mapped
public StrategyDefinition Definition { get; set; } = null!; // dependent → principal
```

- `From(StrategyRuleSet rules)` sets `Id` / `Version` from `rules.VersionId`
  (`rules.VersionId.Id`, `rules.VersionId.Version`).
- `ToDomain()` still calls `StrategyRuleSetSerializer.Deserialize(VersionId, Rules)`.
  Serializer round-trip behavior is unchanged.

### 2. `StrategyDefinition` — `HTB.Strategy.Shared/Domain/StrategyDefinition.cs`

Add the reverse (principal→dependent) navigation:

```csharp
public StrategyRuleSetRow? RuleSet { get; set; }   // principal → dependent
```

**Trade-off (accepted):** this introduces a Domain → Persistence type reference.
Both types live in the same `HTB.Strategy.Shared` assembly, so there is no build
cycle — it is a layering smell, not a structural break. Chosen deliberately so a
definition can `.Include(d => d.RuleSet)` directly.

### 3. EF model — `StrategyDbContextBase.OnModelCreating`

Replace the `strategy_rule_sets` mapping:

```csharp
modelBuilder.Entity<StrategyRuleSetRow>(entity =>
{
    entity.ToTable("strategy_rule_sets", Schema);
    entity.HasKey(r => new { r.Id, r.Version });
    entity.Property(r => r.Id).HasColumnName("id");
    entity.Property(r => r.Version).HasColumnName("version");
    entity.Property(r => r.Rules).HasColumnName("rules").HasColumnType("jsonb").IsRequired();

    entity.HasOne(r => r.Definition)
          .WithOne(d => d.RuleSet)
          .HasForeignKey<StrategyRuleSetRow>(r => new { r.Id, r.Version })
          .OnDelete(DeleteBehavior.Cascade);
});
```

The existing `StrategyId` / `StrategyVersion` value-conversion conventions cover the
new `id` / `version` columns. The `StrategyVersionId` conversion convention stays
registered (harmless; the `version_id` column is gone).

### 4. Migration — regenerate `AddStrategyRuleSets`

The `AddStrategyRuleSets` migration is unreleased, same-branch, same-day work. Rather
than stack a corrective migration, regenerate it so the final table shape lives in one
clean migration:

1. `dotnet ef migrations remove` (drops the staged `AddStrategyRuleSets` files and
   rewinds the snapshot).
2. Re-add with `dotnet ef migrations add AddStrategyRuleSets` against the updated model.

Resulting schema for `strategy.strategy_rule_sets`:

- columns: `id text`, `version int`, `rules jsonb not null`
- primary key: `(id, version)`
- foreign key: `(id, version) → strategy.strategy_definitions(id, version)` `ON DELETE CASCADE`

### 5. Tests (repo 100%-coverage rule)

Mirror existing strategy-persistence test patterns:

- `StrategyRuleSetRow.From` / `ToDomain` round-trip with the split `Id` / `Version`
  identity (and `VersionId` computed property).
- EF model assertion: the 1:1 relationship exists with foreign key `(id, version)`,
  cascade delete, and both navigations (`StrategyRuleSetRow.Definition`,
  `StrategyDefinition.RuleSet`).
- Serializer round-trip unchanged (regression guard).

## Acceptance criteria

- `strategy_rule_sets` is keyed `(id, version)` with an FK to `strategy_definitions`
  and `ON DELETE CASCADE`.
- Deleting a definition cascades to its rule set; inserting a rule set whose
  `(id, version)` has no definition is rejected by the DB.
- `StrategyRuleSetRow` ↔ `StrategyRuleSet` round-trips unchanged.
- One regenerated `AddStrategyRuleSets` migration produces the schema above; snapshot
  matches the model.
- Solution builds; tests pass at 100% coverage.
