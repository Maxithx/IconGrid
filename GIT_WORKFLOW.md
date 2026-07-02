# Git Workflow For IconGrid

This guide is the shared workflow for future work after `refactor-mainwindow` is merged into `main`.

## Goal

Use `main` as the stable source of truth.

Do not keep building new work directly on long-lived refactor branches after the refactor is done.

## What Happens After Merge

When `refactor-mainwindow` is merged into `main`:

1. `main` becomes the correct and current branch again.
2. New work should start from `main`.
3. `refactor-mainwindow` can be treated as finished history.
4. Future changes should be made in short-lived branches.

## Best Practice

For almost all future work:

1. Update local `main`
2. Create a new branch from `main`
3. Make one focused change
4. Test it
5. Commit it
6. Push it
7. Merge it back into `main`

## Branch Naming

Use simple branch names that describe the change.

Examples:

- `fix/layout-tooltip-language`
- `fix/hardware-page-null-state`
- `feat/custom-icon-pack`
- `feat/export-layouts`
- `docs/update-readme-screenshots`
- `refactor/layout-menu-cleanup`

## Recommended Flow For Small Changes

Example for a small fix:

```powershell
git checkout main
git pull origin main
git checkout -b fix/my-small-change
```

Make the change, test it, then:

```powershell
git add .
git commit -m "fix: describe the change"
git push origin fix/my-small-change
```

Then merge that branch into `main` on GitHub, or locally if you prefer.

## Recommended Flow For Larger Changes

For bigger work, still branch from `main`, but keep the branch focused on one feature area.

Good:

- one feature
- one refactor
- one bug cluster

Avoid:

- mixing refactor, docs, assets, and unrelated bugfixes in one branch unless they clearly belong together

## How Me And Codex Should Work Together

Use this workflow when asking Codex for help:

1. Start from `main`
2. Create a branch for the specific task
3. Ask Codex to work only on that task
4. Test the result
5. Commit and push when stable

Good examples:

- "Create a branch for this fix and localize the launcher menu"
- "We are on `main`; make a new feature branch for hardware page cleanup"
- "Review this branch before we merge to `main`"

## Commit Style

Use short focused commit messages.

Examples:

- `fix: localize floating icon exit menu`
- `feat: add language switcher to settings home`
- `docs: refresh English screenshots`
- `refactor: modularize settings window coordination`

## When To Commit

Commit when the change is:

- coherent
- tested
- understandable on its own

Do not wait too long if the work is already stable.

## When To Split Commits

Split commits when changes are logically separate.

Example:

- one commit for code localization
- one commit for updated screenshots

That keeps history cleaner and makes rollbacks safer.

## Merge Strategy

Preferred default:

- merge branch into `main`
- delete the feature branch after merge

That keeps GitHub clean and makes it obvious what is still active.

## Practical Rule For This Repo

From now on, treat:

- `main` as stable truth
- short-lived branches as working branches
- old refactor branches as completed phases

## Simple Default Checklist

Before starting:

1. `git checkout main`
2. `git pull origin main`
3. `git checkout -b <new-branch-name>`

Before finishing:

1. test the change
2. `git status`
3. `git add` only the intended files
4. `git commit -m "..."`
5. `git push origin <branch>`
6. merge to `main`

