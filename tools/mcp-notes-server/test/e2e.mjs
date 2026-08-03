#!/usr/bin/env node
// End-to-end test for the icongrid-notes MCP server.
// Spawns the server from this repo copy over stdio (same transport Cline uses)
// and verifies: initialize -> tools/list -> tools/call
//   (list_notes, update_note append/replace, append_to_note, replace_in_note,
//    check_version_consistency, check_architecture_rules)
//
// Usage:
//   node test/e2e.mjs
//   node test/e2e.mjs --workspace "D:/some/other/IconGrid-clone"
//
// Exit code 0 = all tests passed, 1 = failure.
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'node:fs/promises';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const serverPath = path.join(scriptDir, '..', 'src', 'index.js');
const workspaceArgIndex = process.argv.indexOf('--workspace');
const workspace = workspaceArgIndex !== -1
  ? process.argv[workspaceArgIndex + 1]
  : (process.env.ICONGRID_WORKSPACE ?? 'E:\\IconGrid-GitHub');

// A throwaway note used only to test the write tools. Created before the calls
// and deleted afterwards so existing notes are never modified.
const TEST_NOTE = path.join(workspace, '.local-state', '__e2e_update_test.md');

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

async function callTool(name, args) {
  const id = send('tools/call', { name, arguments: args });
  const res = await waitForId(id);
  return res;
}

async function main() {
  console.log(`Testing server: ${serverPath}`);
  console.log(`Workspace:      ${workspace}`);
  let cleanNote = false;
  try {
    // Prepare throwaway test note.
    await fs.mkdir(path.dirname(TEST_NOTE), { recursive: true });
    await fs.writeFile(TEST_NOTE, '# E2E test note\n\nExisting content here.\n', 'utf8');
    cleanNote = true;

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
    const expected = ['list_notes', 'read_note', 'search_notes', 'append_to_note', 'replace_in_note', 'update_note', 'check_architecture_rules', 'check_version_consistency'];
    check(tools.length >= expected.length, `tools/list -> ${tools.length} tools (${names.join(', ')})`);
    for (const name of expected) {
      check(names.includes(name), `tools/list includes "${name}"`);
    }

    // Verify required fields on the dedicated tools' schemas.
    const appendSchema = tools.find((t) => t.name === 'append_to_note')?.inputSchema;
    check(
      JSON.stringify(appendSchema?.required) === JSON.stringify(['note', 'heading', 'content']),
      'append_to_note schema requires note, heading, content'
    );
    const replaceSchema = tools.find((t) => t.name === 'replace_in_note')?.inputSchema;
    check(
      JSON.stringify(replaceSchema?.required) === JSON.stringify(['note', 'find', 'content']),
      'replace_in_note schema requires note, find, content'
    );

    // 3. tools/call list_notes
    const call = await callTool('list_notes', {});
    const text = (call.result?.content?.[0]?.text) || '';
    check(!call.result?.isError, 'list_notes -> no error');
    check(text.includes('CHAT_STATE.md'), 'list_notes includes CHAT_STATE.md');

    // 4. update_note with operation=append (creates heading if missing)
    const updAppend = await callTool('update_note', {
      note: '__e2e_update_test',
      operation: 'append',
      heading: 'Test heading',
      content: '- appended line via update_note',
    });
    check(!updAppend.result?.isError, 'update_note append -> no error');
    const updAppendText = (updAppend.result?.content?.[0]?.text) || '';
    check(updAppendText.includes('Appended content under heading "Test heading"'), 'update_note append -> summary text');

    // 5. update_note with operation=replace
    const updReplace = await callTool('update_note', {
      note: '__e2e_update_test',
      operation: 'replace',
      find: 'Existing content here.',
      content: 'Replaced content here.',
    });
    check(!updReplace.result?.isError, 'update_note replace -> no error');
    const updReplaceText = (updReplace.result?.content?.[0]?.text) || '';
    check(updReplaceText.includes('Replaced text'), 'update_note replace -> summary text');

    // 6. append_to_note (dedicated tool)
    const append = await callTool('append_to_note', {
      note: '__e2e_update_test',
      heading: 'Dedicated heading',
      content: '- appended via append_to_note',
    });
    check(!append.result?.isError, 'append_to_note -> no error');
    const appendText = (append.result?.content?.[0]?.text) || '';
    check(appendText.includes('Dedicated heading'), 'append_to_note -> summary text');

    // 7. replace_in_note (dedicated tool)
    const replace = await callTool('replace_in_note', {
      note: '__e2e_update_test',
      find: 'Replaced content here.',
      content: 'Double replaced content.',
    });
    check(!replace.result?.isError, 'replace_in_note -> no error');
    const replaceText = (replace.result?.content?.[0]?.text) || '';
    check(replaceText.includes('Replaced text'), 'replace_in_note -> summary text');

    // 8. Verify the note content reflects all write operations.
    const readRes = await callTool('read_note', { note: '__e2e_update_test' });
    const noteContent = (readRes.result?.content?.[0]?.text) || '';
    check(!readRes.result?.isError, 'read_note after writes -> no error');
    check(noteContent.includes('Double replaced content.'), 'note content -> replace_in_note applied');
    check(noteContent.includes('- appended via append_to_note'), 'note content -> append_to_note applied');
    check(noteContent.includes('- appended line via update_note'), 'note content -> update_note append applied');

    // 9. update_note with missing heading for append -> clear error
    const badAppend = await callTool('update_note', {
      note: '__e2e_update_test',
      operation: 'append',
      content: 'no heading here',
    });
    const badAppendMsg = (badAppend.error?.message) || (badAppend.result?.content?.[0]?.text) || '';
    check(
      badAppend.error !== undefined || badAppend.result?.isError === true,
      'update_note append without heading -> error'
    );

    // 10. update_note with missing find for replace -> clear error
    const badReplace = await callTool('update_note', {
      note: '__e2e_update_test',
      operation: 'replace',
      content: 'no find here',
    });
    check(
      badReplace.error !== undefined || badReplace.result?.isError === true,
      'update_note replace without find -> error'
    );

    // 11. check_version_consistency (happy path)
    const ver = await callTool('check_version_consistency', {});
    check(!ver.result?.isError, 'check_version_consistency -> no error');

    // 12. check_architecture_rules (happy path)
    const arch = await callTool('check_architecture_rules', { includeOk: false });
    check(!arch.result?.isError, 'check_architecture_rules -> no error');

    console.log('\n=== ALL TESTS PASSED ===');
    child.kill();
    await fs.rm(TEST_NOTE, { force: true });
    process.exit(0);
  } catch (err) {
    console.error(`\n=== TEST FAILED: ${err.message} ===`);
    child.kill();
    if (cleanNote) {
      await fs.rm(TEST_NOTE, { force: true }).catch(() => {});
    }
    process.exit(1);
  }
}

main();