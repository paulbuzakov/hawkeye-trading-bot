---
name: feature-new
description: Start new work in the hawkeye-trading-bot repo by creating a conventionally-typed branch (feat/fix/docs/... <name>) off main. Use when the user wants to begin a new feature, fix, docs change, or other work on a fresh branch.
---

# feature-new

Creates a new branch using the repo's `<type>/<name>` naming convention. That is all this skill does.

## Branch types

Use a Conventional Commits type as the branch prefix:

| Type       | Use for                                              |
|------------|------------------------------------------------------|
| `feat`     | a new feature                                        |
| `fix`      | a bug fix                                             |
| `docs`     | documentation-only changes                           |
| `refactor` | code change that neither fixes a bug nor adds a feature |
| `test`     | adding or correcting tests                           |
| `perf`     | a performance improvement                            |
| `chore`    | tooling, deps, config, maintenance                   |
| `build`    | build system or external dependency changes          |
| `ci`       | CI configuration changes                             |
| `style`    | formatting / whitespace, no behavior change          |

## Inputs

Ask the user if not given:
- **Type** — one of the types above. Default to `feat` for new functionality. The user may also say a `<type>` directly (e.g. "fix branch for ...").
- **Name** — short kebab-case slug (e.g. `order-router`).

## Steps

1. **Confirm starting point.** Ensure the working tree is clean and you are on `main` (or the agreed base). If there are uncommitted changes, stop and ask before branching.

2. **Create the branch** using `<type>/<name>`:
   ```
   git checkout -b <type>/<name>
   ```

3. **Report** the new branch name and confirm you are on it.
