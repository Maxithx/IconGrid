# IconGrid Agent Guide

This file is the entry point for any agent working in this repository.

## Read first

Before making changes, read these files in this order:

1. `CHAT_STATE.md`
2. `README.md`
3. `ARCHITECTURE_RULES.md`

## Purpose

- `CHAT_STATE.md` is the temporary source of truth for the current debugging state.
- `README.md` describes the intended application behavior and project structure.
- `ARCHITECTURE_RULES.md` contains the constraints for keeping the codebase modular and stable.

## Working rules

- Treat `CHAT_STATE.md` as the live note for unresolved issues and recent findings.
- Do not rely on chat history as source of truth.
- If startup or elevation behavior is involved, verify it against the actual code and Windows state.
- If Windows startup creates a duplicate or elevated IconGrid instance, inspect Task Scheduler first for a stale `IconGrid` task before changing application code.
- Keep changes small and verify them with a build before claiming the issue is fixed.
- An active GitHub CLI (`gh`) login is not required for `git commit` or `git push`; use the repository's configured Git credentials. Only require `gh` authentication for GitHub CLI operations such as creating pull requests.

## Current focus

- Fix Windows startup so IconGrid no longer launches extra elevated instances.
- Remove or repair stale Windows startup entries when they are found.
- Preserve the separation between launcher UI and hardware monitor behavior.
