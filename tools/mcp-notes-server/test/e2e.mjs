#!/usr/bin/env node
// End-to-end test for the icongrid-notes MCP server.
// Spawns the server from this repo copy over stdio (same transport Cline uses)
// and verifies: initialize -> tools/list -> tools/call (list_notes).
//
// Usage:
//   node test/e2e.mjs
//   node test/e2e.mjs --workspace "D:/some/other/IconGrid-clone"
//
// Exit code 0 = all tests passed, 1 = failure.
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const serverPath = path.join(scriptDir, '..', 'src', 'index.js');
const workspaceArgIndex = process.argv.indexOf('--workspace');
const workspace = workspaceArgIndex !== -1
  ? process.argv[workspaceArgIndex + 1]
  : (process.env.ICONGRID_WORKSPACE ?? 'E:\\IconGrid-GitHub');

const child = spawn(process.execPath, [serverPath], {
  stdio: ['pipe', 'pipe', 'pipe'],
  env: { ...process.env, ICONGRID_WORKSPACE: workspace },
});

let buffer = '';
const responses = [];
let nextId = 1;

child.stdout.on('data', (chunk) => {
  buffer += chunk.toString();
  let idx;
  while ((idx = buffer.indexOf('\n')) !== -1) {
    const line = buffer.slice(0, idx).trim();
    buffer = buffer.slice(idx + 1);
    if (!line) continue;
    try {
      const msg = JSON.parse(line);
      if (msg.id !== undefined && msg.id !== null) {
        responses.push(msg);
      }
    } catch {
      // Not JSON (e.g. a stray log line) — ignore.
    }
  }
});

child.stderr.on('data', (d) => process.stderr.write(`[stderr] ${d}`));

function send(method, params = {}) {
  const id = nextId++;
  child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n');
  return id;
}

function waitForId(id, timeoutMs = 10000) {
  return new Promise((resolve, reject) => {
    const start = Date.now();
    const timer = setInterval(() => {
      const found = responses.find((r) => r.id === id);
      if (found) {
        clearInterval(timer);
        resolve(found);
      } else if (Date.now() - start > timeoutMs) {
        clearInterval(timer);
        reject(new Error(`Timeout waiting for response id ${id}`));
      }
    }, 50);
  });
}

function check(condition, label) {
  if (!condition) throw new Error(`FAILED: ${label}`);
  console.log(`[PASS] ${label}`);
}

async function main() {
  console.log(`Testing server: ${serverPath}`);
  console.log(`Workspace:      ${workspace}`);
  try {
    // 1. initialize
    const initId = send('initialize', {
      protocolVersion: '2024-11-05',
      capabilities: {},
      clientInfo: { name: 'mcp-e2e-test', version: '1.0.0' },
    });
    const init = await waitForId(initId);
    check(init.result && init.result.serverInfo, `initialize -> serverInfo ${JSON.stringify(init.result.serverInfo)}`);
    check((init.result?.protocolVersion ?? '') === '2024-11-05', 'initialize -> protocolVersion 2024-11-05');
    check(init.result.capabilities?.tools, 'initialize -> capabilities.tools present');

    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }) + '\n');

    // 2. tools/list
    const listId = send('tools/list');
    const list = await waitForId(listId);
    const tools = (list.result && list.result.tools) || [];
    const names = tools.map((t) => t.name);
    const expected = ['list_notes', 'read_note', 'search_notes', 'update_note', 'check_architecture_rules', 'check_version_consistency'];
    check(tools.length >= expected.length, `tools/list -> ${tools.length} tools (${names.join(', ')})`);
    for (const name of expected) {
      check(names.includes(name), `tools/list includes "${name}"`);
    }

    // 3. tools/call list_notes
    const callId = send('tools/call', { name: 'list_notes', arguments: {} });
    const call = await waitForId(callId);
    const text = (call.result?.content?.[0]?.text) || '';
    check(!call.result?.isError, 'list_notes -> no error');
    check(text.includes('CHAT_STATE.md'), 'list_notes includes CHAT_STATE.md');

    // 4. tools/call check_version_consistency (happy path)
    const verId = send('tools/call', { name: 'check_version_consistency', arguments: {} });
    const ver = await waitForId(verId);
    const verText = (ver.result?.content?.[0]?.text) || '';
    check(!ver.result?.isError, 'check_version_consistency -> no error');

    // 5. tools/call check_architecture_rules (happy path)
    const archId = send('tools/call', { name: 'check_architecture_rules', arguments: { includeOk: false } });
    const arch = await waitForId(archId);
    const archText = (arch.result?.content?.[0]?.text) || '';
    check(!arch.result?.isError, 'check_architecture_rules -> no error');

    console.log('\n=== ALL TESTS PASSED ===');
    child.kill();
    process.exit(0);
  } catch (err) {
    console.error(`\n=== TEST FAILED: ${err.message} ===`);
    child.kill();
    process.exit(1);
  }
}

main();