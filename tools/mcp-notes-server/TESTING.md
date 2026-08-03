# Testing — icongrid-notes MCP server

How to verify the server works. Test results are logged in `CHAT_STATE.md` (session memory), not in a separate file.

## Prerequisites

- Node.js installed
- `npm install` has been run in `tools/mcp-notes-server/` (creates `node_modules/`)

## 1. Automated end-to-end test (recommended)

Runs the full MCP protocol over stdio — the same transport Cline uses — and verifies:
`initialize` → `tools/list` → `tools/call` (`list_notes`, `check_version_consistency`, `check_architecture_rules`).

```powershell
cd tools\mcp-notes-server
node test\e2e.mjs
```

- Exit code `0` + `=== ALL TESTS PASSED ===` = OK
- Exit code `1` + `=== TEST FAILED: ... ===` = failure — fix before committing

The server reads notes from the workspace set by `ICONGRID_WORKSPACE` (defaults to `E:\IconGrid-GitHub`).
To test against a different checkout:

```powershell
node test\e2e.mjs --workspace "D:\other\IconGrid-clone"
```

## 2. Manual test in Cline

1. Reload Cline (or restart the extension) so the MCP server reconnects.
2. Check the MCP server shows as connected (icon / status for `icongrid-notes`).
3. Ask Cline to: **"list the notes with the icongrid-notes tools"** — it should return `CHAT_STATE.md` plus the `.local-state` notes.
4. Ask Cline to: **"append a line under a test heading to CHAT_STATE"** — then confirm the text is visible in `CHAT_STATE.md`.
5. Ask Cline to: **"run check_version_consistency and check_architecture_rules"** — both should return structured reports without tool errors.

## 3. Quick smoke test from the terminal (optional)

Spawn the server and send a single `tools/list` request:

```powershell
cd tools\mcp-notes-server
@'{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0.0"}}}' | node src\index.js
```

Expected output contains:
`"serverInfo":{"name":"icongrid-notes-server","version":"0.1.0"}`

## When to run tests

- After any change to `tools/mcp-notes-server/src/`
- After a fresh clone of the repo (verify the checkout is self-sufficient)
- Before committing server changes
- At session end, log the result in `CHAT_STATE.md` under "MCP server test"