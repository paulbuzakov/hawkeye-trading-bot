---
name: feature-commit
description: Ship the current branch in the hawkeye-trading-bot repo — commit with a Conventional Commits message, push, and open a PR. Use when the user wants to commit/ship/finish work, open a PR, or wrap up a <type>/<name> branch (feat/fix/docs/...).
---

# feature-commit

Ships the current `<type>/<name>` branch: commit → push → open PR. Pairs with the `feature-new` flow.

## Commit / branch types

Use Conventional Commits types — these match the `feature-new` branch prefixes:

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

Infer the type from the branch prefix (`<type>/<name>`); if it doesn't match, use the type that best describes the change.

## Preconditions

1. **On a work branch.** Confirm you are on a `<type>/<name>` branch, not `main`. If on `main`, stop and ask.
2. **Conventions hold.** Code lives in `src/`, docs in `docs/`, tests in `tests/` (never `test/`).
3. **Tests pass at 100% coverage.** Every line of `src/` must reach 100% line + branch coverage — CI blocks PRs below that. Build and run the test suite with coverage before committing. If anything fails or coverage is below 100%, stop and report; do not ship. (Skip the coverage gate only for `docs`/`ci`/`chore` changes that touch no `src/` code.)

## Steps

1. **Review the diff.** Run `git status` and `git diff` to see what's being shipped. Confirm only intended files are staged.

2. **Commit** using a Conventional Commits message: `<type>(<scope>): <summary>` (scope optional, e.g. `feat(marketdata): add order router`).

3. **Push** the branch to the remote (set upstream on first push):
   ```
   git push -u origin <type>/<name>
   ```

4. **Open a PR** with `gh pr create`, targeting `main`. Title it with the same Conventional Commits summary. Body summarizes the change, the test coverage, and anything reviewers should know.

5. **Report** the commit, the pushed branch, and the PR URL.

## Do not

- **Do not leave the work branch.** After the PR is open, stay on `<type>/<name>`. Never run `git checkout main` and never delete the branch (`git branch -D`). The flow ends right after opening the PR and reporting — there is no "return to main" or "clean up" step.
- Do not ship code if tests fail or coverage is below 100%.
