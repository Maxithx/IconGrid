# Versioning Strategy

IconGrid uses a simple **Semantic Versioning** model with beta builds during active development and refactoring.

## Version Format

`MAJOR.MINOR.PATCH[-beta.X]`

Examples:
- `0.7.0-beta.1`
- `0.7.0-beta.2`
- `0.7.0`
- `0.7.1`
- `1.0.0`

## Meaning

- `MAJOR`: Breaking changes or large architectural shifts that change compatibility or expected behavior.
- `MINOR`: New features or meaningful functional improvements that remain backward compatible.
- `PATCH`: Bug fixes, small polish changes, or safe maintenance updates.
- `-beta.X`: A test build that is not considered final yet.

## IconGrid Rules

While IconGrid is still undergoing larger refactoring work, the project should stay on:

`0.x.x-beta.x`

That means:
- Use `beta` versions while launcher, settings, layout handling, and ViewModel cleanup are still moving.
- Increase `MINOR` when a larger functional area is completed.
- Increase `PATCH` for focused fixes that do not change overall scope.
- Increase the beta number for each new internal test build of the same planned release.

## Recommended Flow

Example flow:

- `0.7.0-beta.1` = first test build for a launcher refactor milestone
- `0.7.0-beta.2` = same milestone, but with fixes after testing
- `0.7.0` = stable release for that milestone
- `0.7.1` = stable bug-fix release
- `0.8.0-beta.1` = next larger feature/refactor cycle

## When to Use `1.0.0`

Move to `1.0.0` when these areas are considered stable:

- Main Launcher Dashboard
- Settings & Control Dashboard
- Layout system
- Persistence/config flow
- Hardware monitor behavior

## Where the Version Should Appear

The version should eventually be kept consistent in:

- `IconGrid.csproj`
- `README.md`
- the `Om` / About page in the app

## Maintenance Rules

Do not update every documentation file after every small change. Each file has a different purpose.

### Update `IconGrid.csproj` when

- a new beta build is created
- a milestone is considered release-ready
- a stable release is published

### Update `README.md` when

- user-facing behavior changes
- architecture changes are important enough to document
- new controls, modules, or workflows are introduced

### Update `TODO.md` when

- the active refactor plan changes
- a phase is completed
- the next engineering steps need to be clarified

### Update `VERSIONING.md` when

- the versioning policy changes
- the release flow changes
- the team decides to use a different versioning model

## Recommended Workflow

Use this practical rule set:

- small refactor: update `TODO.md` only if the plan changed
- completed milestone: update `IconGrid.csproj`, and update `README.md` if documentation changed
- version-policy change: update `VERSIONING.md`

This keeps documentation maintenance focused and avoids unnecessary churn.

## Practical Recommendation Right Now

Use a version such as:

`0.7.0-beta.1`

That signals:
- the app is usable
- the project is actively being tested
- the architecture is still being improved before a stable `1.0.0`
