#!/usr/bin/env node
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ErrorCode,
  ListToolsRequestSchema,
  McpError,
} from '@modelcontextprotocol/sdk/types.js';
import fs from 'node:fs/promises';
import path from 'node:path';

// Root of the IconGrid workspace where notes live.
const WORKSPACE_ROOT = process.env.ICONGRID_WORKSPACE ?? 'E:\\IconGrid-GitHub';
const CHAT_STATE_PATH = path.join(WORKSPACE_ROOT, 'CHAT_STATE.md');
const LOCAL_STATE_DIR = path.join(WORKSPACE_ROOT, '.local-state');

async function fileExists(filePath) {
  try {
    const stat = await fs.stat(filePath);
    return stat.isFile();
  } catch {
    return false;
  }
}

function resolveNotePath(name) {
  const trimmed = (name ?? '').trim();
  if (!trimmed) {
    throw new McpError(ErrorCode.InvalidParams, 'note name is required');
  }

  // Allow plain names like "fps-etw", "fps-etw.md", "CHAT_STATE", "CHAT_STATE.md".
  let fileName = trimmed;
  if (!fileName.toLowerCase().endsWith('.md')) {
    fileName = `${fileName}.md`;
  }

  const isChatState = fileName.toLowerCase() === 'chat_state.md';
  const target = isChatState
    ? CHAT_STATE_PATH
    : path.join(LOCAL_STATE_DIR, fileName);

  return { target, fileName, isChatState };
}

async function listNoteFiles() {
  const notes = [];
  if (await fileExists(CHAT_STATE_PATH)) {
    notes.push({
      name: 'CHAT_STATE.md',
      path: CHAT_STATE_PATH,
      kind: 'tracked',
    });
  }
  try {
    const entries = await fs.readdir(LOCAL_STATE_DIR, { withFileTypes: true });
    for (const entry of entries) {
      if (entry.isFile() && entry.name.toLowerCase().endsWith('.md')) {
        notes.push({
          name: entry.name,
          path: path.join(LOCAL_STATE_DIR, entry.name),
          kind: 'local-state',
        });
      }
    }
  } catch {
    // local-state dir may not exist; skip silently.
  }
  return notes;
}

// Structured update of a markdown note.
// - append: append content under a heading (creates heading if missing)
// - replace: find/replace of exact text (first occurrence)
const isValidAppendArgs = (args) =>
  typeof args === 'object' &&
  args !== null &&
  typeof args.note === 'string' &&
  args.note.trim().length > 0 &&
  typeof args.heading === 'string' &&
  args.heading.trim().length > 0 &&
  typeof args.content === 'string';

const isValidReplaceArgs = (args) =>
  typeof args === 'object' &&
  args !== null &&
  typeof args.note === 'string' &&
  args.note.trim().length > 0 &&
  typeof args.find === 'string' &&
  args.find.length > 0 &&
  typeof args.content === 'string';

// Legacy update_note: operation + note + content + (heading for append | find for replace)
const isValidUpdateArgs = (args) =>
  typeof args === 'object' &&
  args !== null &&
  typeof args.note === 'string' &&
  args.note.trim().length > 0 &&
  (args.operation === 'append' || args.operation === 'replace') &&
  typeof args.content === 'string' &&
  (args.operation === 'append'
    ? typeof args.heading === 'string' && args.heading.trim().length > 0
    : typeof args.find === 'string' && args.find.length > 0);

// Returns a specific, actionable error message for invalid update_note args.
function buildUpdateArgsError(args) {
  if (typeof args !== 'object' || args === null) {
    return 'update_note requires an arguments object with note, operation, content (and heading for append / find for replace)';
  }
  if (typeof args.note !== 'string' || !args.note.trim()) {
    return 'note (string) is required for update_note';
  }
  if (args.operation !== 'append' && args.operation !== 'replace') {
    return 'operation must be "append" or "replace" for update_note';
  }
  if (typeof args.content !== 'string') {
    return 'content (string) is required for update_note';
  }
  if (args.operation === 'append') {
    if (typeof args.heading !== 'string' || !args.heading.trim()) {
      return 'heading is required when operation=append';
    }
  } else if (typeof args.find !== 'string' || !args.find) {
    return 'find is required when operation=replace';
  }
  return 'Invalid update_note arguments';
}

// ---- Architecture rules check ----

// Thresholds derived from ARCHITECTURE_RULES.md intent:
// - MainWindow should stay small (shell only)
// - MainViewModel should stay a composition root, not a feature dump
// - Generic guard: no single file should grow beyond 1500 lines
const ARCH_CHECKS = [
  {
    file: 'Views/Launcher/MainWindow.xaml.cs',
    maxLines: 1000,
    label: 'MainWindow code-behind (ARCHITECTURE_RULES.md: MainWindow Rules)',
  },
  {
    file: 'ViewModels/MainViewModel.cs',
    maxLines: 1200,
    label: 'MainViewModel (ARCHITECTURE_RULES.md: MainViewModel Rules)',
  },
  {
    file: 'Helpers/Hardware/HardwareMonitorAgent.cs',
    maxLines: 1500,
    label: 'HardwareMonitorAgent (background worker, keep focused)',
  },
  {
    file: 'Helpers/Launcher/SystemMonitor.cs',
    maxLines: 1000,
    label: 'SystemMonitor (launcher-side monitor)',
  },
];

// Count method-like declarations in a C# file (rough heuristic).
function countMethods(text) {
  const lines = text.split(/\r?\n/);
  let count = 0;
  for (const line of lines) {
    const trimmed = line.trim();
    // Skip comments, properties, control flow, field declarations.
    if (
      trimmed.startsWith('//') ||
      trimmed.startsWith('/*') ||
      trimmed.startsWith('*') ||
      trimmed.startsWith('///') ||
      trimmed.startsWith('#') ||
      trimmed.startsWith('public class') ||
      trimmed.startsWith('internal class') ||
      trimmed.startsWith('private class') ||
      trimmed.startsWith('public partial class') ||
      trimmed.startsWith('internal partial class') ||
      trimmed.startsWith('private partial class')
    ) {
      continue;
    }
    // Heuristic: a line ending with ")" on a method-like signature, or
    // a line starting with access modifier + return type + name + "(".
    const methodLike = /(?:public|private|protected|internal)\s+(?:static\s+|async\s+|virtual\s+|override\s+|sealed\s+|readonly\s+|partial\s+)*(?:[A-Za-z_][A-Za-z0-9_<>,.?\[\]\s]*)\s+[A-Za-z_][A-Za-z0-9_]*\s*\(/.test(
      trimmed
    );
    // Exclude properties and constructors-ish lines that end with "=>" (expression-bodied property).
    const isExpressionProperty = /\s*=>\s*/.test(trimmed) && !/\(\)\s*=>/.test(trimmed);
    if (methodLike && !isExpressionProperty) {
      count++;
    }
  }
  return count;
}

async function architectureReport(includeOk = false) {
  const results = [];
  for (const check of ARCH_CHECKS) {
    const fullPath = path.join(WORKSPACE_ROOT, check.file);
    if (!(await fileExists(fullPath))) {
      results.push({
        file: check.file,
        status: 'missing',
        lines: null,
        limit: check.maxLines,
      });
      continue;
    }
    const text = await fs.readFile(fullPath, 'utf8');
    const lineCount = text.split(/\r?\n/).length;
    const methodCount = countMethods(text);
    const limit = check.maxLines;
    const status = lineCount > limit ? 'violation' : 'ok';
    results.push({
      file: check.file,
      status,
      lines: lineCount,
      limit,
      methods: methodCount,
    });
  }

  const lines = [];
  const violations = results.filter((r) => r.status === 'violation');
  for (const r of results) {
    if (r.status === 'missing') {
      lines.push(`? ${r.file} (missing)`);
      continue;
    }
    if (r.status === 'ok' && !includeOk) {
      continue;
    }
    const marker = r.status === 'violation' ? 'VIOLATION' : 'ok';
    lines.push(
      `${marker} ${r.file}: ${r.lines} lines (limit ${r.limit}) | ~${r.methods} methods`
    );
  }

  if (violations.length === 0) {
    lines.push('All checked files are within ARCHITECTURE_RULES.md limits.');
  } else {
    lines.push('');
    lines.push('Suggested follow-ups:');
    for (const v of violations) {
      const suggestions = {
        'Views/Launcher/MainWindow.xaml.cs':
          'Move layout-engine methods (BuildSlots, MatchWindowsToSlots, ArrangeWindowsFromPreset) to Helpers/Launcher/WindowLayoutEngine.cs.',
        'ViewModels/MainViewModel.cs':
          'Extract UI/layout measurement state into a dedicated LauncherLayoutState-like class.',
        'Helpers/Hardware/HardwareMonitorAgent.cs':
          'Keep this worker focused on orchestration; move normalization helpers to a separate class if it grows further.',
        'Helpers/Launcher/SystemMonitor.cs':
          'Consider splitting FPS display state from network/hardware polling.',
      };
      lines.push(`- ${v.file}: ${suggestions[v.file] ?? 'Review and split into a focused class.'}`);
    }
  }

  return lines.join('\n');
}

// ---- Localization completeness check ----
// Verifies that ALL keys in the "en" dictionary also exist in "da" (and vice versa).

async function localizationReport() {
  const fullPath = path.join(WORKSPACE_ROOT, 'Helpers', 'Settings', 'LocalizationHelper.cs');
  if (!(await fileExists(fullPath))) {
    return 'LocalizationHelper.cs not found.';
  }

  const text = await fs.readFile(fullPath, 'utf8');
  const enStart = text.indexOf('["en"] = new()');
  const daStart = text.indexOf('["da"] = new()');
  if (enStart === -1 || daStart === -1) {
    return 'Could not locate "en" or "da" dictionary blocks in LocalizationHelper.cs.';
  }

  const enBlock = text.slice(enStart, daStart);
  const daBlock = text.slice(daStart);

  const keyPattern = /\["([^"]+)"\]\s*=/g;
  const enKeys = new Set();
  const daKeys = new Set();
  let match;
  while ((match = keyPattern.exec(enBlock)) !== null) {
    enKeys.add(match[1]);
  }
  while ((match = keyPattern.exec(daBlock)) !== null) {
    daKeys.add(match[1]);
  }

  const missingInDa = [...enKeys].filter((k) => !daKeys.has(k) && k !== 'en' && k !== 'da');
  const missingInEn = [...daKeys].filter((k) => !enKeys.has(k) && k !== 'en' && k !== 'da');

  const lines = [];
  lines.push(`en keys: ${enKeys.size} | da keys: ${daKeys.size}`);
  if (missingInDa.length === 0 && missingInEn.length === 0) {
    lines.push('All localization keys are synchronized between en and da.');
  } else {
    if (missingInDa.length > 0) {
      lines.push(`Keys in en but MISSING in da (${missingInDa.length}): ${missingInDa.join(', ')}`);
    }
    if (missingInEn.length > 0) {
      lines.push(`Keys in da but MISSING in en (${missingInEn.length}): ${missingInEn.join(', ')}`);
    }
  }
  return lines.join('\n');
}

// ---- Git security check ----
// Scans tracked files for secrets (.pfx, .key, .pem, .env).

async function gitSecurityReport() {
  const { exec } = await import('node:child_process');
  return new Promise((resolve) => {
    exec('git ls-files', { cwd: WORKSPACE_ROOT }, (err, stdout) => {
      if (err) {
        resolve(`Git security check failed: ${err.message}`);
        return;
      }
      const files = stdout.split(/\r?\n/).filter(Boolean);
      const dangerous = files.filter((f) =>
        /\.(pfx|key|pem)$/i.test(f) || /\.env(\..*)?$/i.test(f)
      );
      if (dangerous.length === 0) {
        resolve('No staged secrets found (pfx/key/pem/env).');
      } else {
        resolve(
          `DANGER — staged secrets found (${dangerous.length}):\n${dangerous.join('\n')}\nRemove these files from git tracking immediately.`
        );
      }
    });
  });
}

// ---- XAML hardcoded Danish check ----
// Scans XAML files for hardcoded Danish text (Text="...æøå...") not using localization bindings.

async function xamlDanishReport() {
  const xamlDirs = ['Views', 'Controls'];
  const results = [];

  async function scanDir(dirPath) {
    const fullDir = path.join(WORKSPACE_ROOT, dirPath);
    try {
      const entries = await fs.readdir(fullDir, { withFileTypes: true, recursive: true });
      for (const entry of entries) {
        if (entry.isFile() && entry.name.toLowerCase().endsWith('.xaml')) {
          const filePath = path.join(entry.parentPath || fullDir, entry.name);
          const text = await fs.readFile(filePath, 'utf8');
          const lines = text.split(/\r?\n/);
          for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const danishMatch = line.match(/Text="([^"]*[æøåÆØÅ][^"]*)"/);
            if (danishMatch && !line.includes('{Binding')) {
              results.push(`${path.relative(WORKSPACE_ROOT, filePath)}:${i + 1}: ${line.trim()}`);
            }
          }
        }
      }
    } catch {
      // Directory may not exist — skip.
    }
  }

  for (const dir of xamlDirs) {
    await scanDir(dir);
  }

  if (results.length === 0) {
    return 'No hardcoded Danish text found in XAML files.';
  }
  return `Hardcoded Danish text in XAML (${results.length} lines) — replace with localized bindings:\n${results.join('\n')}`;
}

// ---- Run all checks ----
// Runs architecture, version, localization, git security, and XAML Danish checks at once.

async function runAllChecks() {
  const results = [];

  results.push('=== Architecture rules ===');
  results.push(await architectureReport(false));
  results.push('');
  results.push('=== Version consistency ===');
  results.push(await versionReport());
  results.push('');
  results.push('=== Localization completeness ===');
  results.push(await localizationReport());
  results.push('');
  results.push('=== Git security ===');
  results.push(await gitSecurityReport());
  results.push('');
  results.push('=== XAML hardcoded Danish ===');
  results.push(await xamlDanishReport());

  return results.join('\n');
}

// ---- Version consistency check ----
// Reads the app version from AssemblyInfo.cs and README.md and reports mismatches.
const VERSION_FILES = [
  {
    path: 'AssemblyInfo.cs',
    label: 'AssemblyInfo.cs',
    pattern: /AssemblyInformationalVersion\("([^"]+)"\)/,
  },
  {
    path: 'README.md',
    label: 'README.md',
    pattern: /Current version:\s*`([^`]+)`/,
  },
];

async function versionReport(includeOk = false) {
  const results = [];
  for (const f of VERSION_FILES) {
    const fullPath = path.join(WORKSPACE_ROOT, f.path);
    if (!(await fileExists(fullPath))) {
      results.push({ label: f.label, status: 'missing', version: null });
      continue;
    }
    const text = await fs.readFile(fullPath, 'utf8');
    const match = text.match(f.pattern);
    results.push({
      label: f.label,
      status: match ? 'ok' : 'no-version-found',
      version: match ? match[1] : null,
    });
  }

  const versions = results
    .filter((r) => r.version)
    .map((r) => ({ label: r.label, version: r.version }));
  const distinct = [...new Set(versions.map((v) => v.version))];

  const lines = [];
  for (const r of results) {
    if (r.status === 'missing') {
      lines.push(`? ${r.label} (missing)`);
      continue;
    }
    if (r.status === 'no-version-found') {
      lines.push(`WARN ${r.label}: version string not found`);
      continue;
    }
    lines.push(`ok ${r.label}: ${r.version}`);
  }

  if (distinct.length <= 1 && results.every((r) => r.status !== 'no-version-found')) {
    lines.push('Version is consistent across all checked files.');
  } else {
    lines.push('');
    lines.push('Version mismatch or missing version string! Fix before release.');
  }

  return lines.join('\n');
}

async function appendToSection(text, heading, content) {
  let lines = text.split(/\r?\n/);
  let headingIndex = -1;

  // Find the heading (case-insensitive, supports "##", "###", etc.)
  for (let i = 0; i < lines.length; i++) {
    if (
      lines[i].trim().toLowerCase().replace(/^#+\s*/, '') ===
      heading.trim().toLowerCase()
    ) {
      headingIndex = i;
      break;
    }
  }

  const insertText = content.endsWith('\n') ? content : `${content}\n`;

  if (headingIndex === -1) {
    // Append a new heading at the end of the file.
    if (lines.length > 0 && lines[lines.length - 1].trim() !== '') {
      lines.push('');
    }
    lines.push(`## ${heading}`);
    lines.push('');
    lines.push(insertText.replace(/\n+$/, ''));
    return lines.join('\n') + '\n';
  }

  // Insert content after the heading's block. The block continues until the
  // next line starting with '#' at the same or lower level, or end of file.
  let insertAt = headingIndex + 1;
  while (insertAt < lines.length) {
    const line = lines[insertAt].trim();
    if (line === '') {
      insertAt++;
      continue;
    }
    const headingMatch = line.match(/^#{1,6}\s+/);
    if (headingMatch) {
      break;
    }
    insertAt++;
  }

  // Skip blank lines before the next section so we insert cleanly.
  let contentStart = insertAt - 1;
  while (contentStart > headingIndex && lines[contentStart].trim() === '') {
    contentStart--;
  }
  lines.splice(contentStart + 1, 0, '', insertText.replace(/\n+$/, ''));
  return lines.join('\n') + '\n';
}

class NotesServer {
  constructor() {
    this.server = new Server(
      {
        name: 'icongrid-notes-server',
        version: '0.1.0',
      },
      {
        capabilities: {
          tools: {},
        },
      }
    );

    this.setupToolHandlers();

    this.server.onerror = (error) => console.error('[MCP Error]', error);
    process.on('SIGINT', async () => {
      await this.server.close();
      process.exit(0);
    });
  }

  setupToolHandlers() {
    this.server.setRequestHandler(ListToolsRequestSchema, async () => ({
      tools: [
        {
          name: 'list_notes',
          description:
            'List all IconGrid notes (CHAT_STATE.md and .local-state/*.md) with their paths.',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
        {
          name: 'read_note',
          description:
            'Read the full content of an IconGrid note. Use names like "CHAT_STATE", "fps-etw", or "fps-etw.md".',
          inputSchema: {
            type: 'object',
            properties: {
              note: {
                type: 'string',
                description:
                  'Note name without or with .md extension, or "CHAT_STATE" for CHAT_STATE.md.',
              },
            },
            required: ['note'],
          },
        },
        {
          name: 'search_notes',
          description:
            'Search all IconGrid notes for a text pattern and return matching lines with file names.',
          inputSchema: {
            type: 'object',
            properties: {
              pattern: {
                type: 'string',
                description: 'Case-insensitive text or regex to search for.',
              },
              note: {
                type: 'string',
                description:
                  'Optional: restrict search to one note (e.g. "fps-etw").',
              },
            },
            required: ['pattern'],
          },
        },
        {
          name: 'append_to_note',
          description:
            'Append content under a markdown heading in an IconGrid note (creates the heading if missing). Preferred over update_note for append operations.',
          inputSchema: {
            type: 'object',
            properties: {
              note: {
                type: 'string',
                description:
                  'Note name without or with .md extension, or "CHAT_STATE" for CHAT_STATE.md.',
              },
              heading: {
                type: 'string',
                description:
                  'Heading text without leading "#" (e.g. "Night session findings").',
              },
              content: {
                type: 'string',
                description:
                  'Content to append under the heading.',
              },
            },
            required: ['note', 'heading', 'content'],
          },
        },
        {
          name: 'replace_in_note',
          description:
            'Find and replace exact text (first occurrence) in an IconGrid note. Preferred over update_note for replace operations.',
          inputSchema: {
            type: 'object',
            properties: {
              note: {
                type: 'string',
                description:
                  'Note name without or with .md extension, or "CHAT_STATE" for CHAT_STATE.md.',
              },
              find: {
                type: 'string',
                description:
                  'Exact text to find (first occurrence).',
              },
              content: {
                type: 'string',
                description:
                  'Replacement text.',
              },
            },
            required: ['note', 'find', 'content'],
          },
        },
        {
          name: 'update_note',
          description:
            'LEGACY alias — preferred: use append_to_note (to append under a heading) or replace_in_note (to find/replace). Append content under a markdown heading, or find/replace exact text, in an IconGrid note.',
          inputSchema: {
            type: 'object',
            properties: {
              note: {
                type: 'string',
                description:
                  'Note name without or with .md extension, or "CHAT_STATE" for CHAT_STATE.md.',
              },
              operation: {
                type: 'string',
                enum: ['append', 'replace'],
                description:
                  'append: add content under a heading (creates it if missing). replace: find/replace exact text.',
              },
              heading: {
                type: 'string',
                description:
                  'Required for append. Heading text without leading "#" (e.g. "Night session findings").',
              },
              find: {
                type: 'string',
                description:
                  'Required for replace. Exact text to find (first occurrence).',
              },
              content: {
                type: 'string',
                description:
                  'Content to append (for append) or replacement text (for replace).',
              },
            },
            required: ['note', 'operation', 'content'],
          },
        },
        {
          name: 'check_architecture_rules',
          description:
            'Check that key source files respect ARCHITECTURE_RULES.md guardrails (file size limits, method counts). Returns a structured report of violations.',
          inputSchema: {
            type: 'object',
            properties: {
              includeOk: {
                type: 'boolean',
                description:
                  'If true, include files that pass the checks too. Default false (only violations).',
              },
            },
          },
        },
        {
          name: 'check_version_consistency',
          description:
            'Check that the app version is consistent across AssemblyInfo.cs and README.md (Current version). Returns a structured report.',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
        {
          name: 'check_localization_completeness',
          description:
            'Check that ALL localization keys in LocalizationHelper.cs exist in BOTH the en and da dictionaries. Reports missing keys.',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
        {
          name: 'check_git_security',
          description:
            'Scan git-tracked files for secrets (pfx, key, pem, env files). Returns a security report.',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
        {
          name: 'check_xaml_hardcoded_danish',
          description:
            'Scan XAML files for hardcoded Danish text (Text="...æøå...") that should use localization bindings instead.',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
        {
          name: 'run_all_checks',
          description:
            'Run ALL architecture, version, localization, security, and XAML checks at once. Use this at session end for a complete project health report.',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
      ],
    }));

    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params;

      switch (name) {
        case 'list_notes': {
          const notes = await listNoteFiles();
          return {
            content: [
              {
                type: 'text',
                text: notes
                  .map((n) => `[${n.kind}] ${n.name} -> ${n.path}`)
                  .join('\n') || 'No notes found.',
              },
            ],
          };
        }

        case 'read_note': {
          if (!args || typeof args.note !== 'string') {
            throw new McpError(
              ErrorCode.InvalidParams,
              'note is required for read_note'
            );
          }
          const { target, fileName } = resolveNotePath(args.note);
          if (!(await fileExists(target))) {
            throw new McpError(
              ErrorCode.InvalidParams,
              `Note not found: ${fileName} (looked at ${target})`
            );
          }
          const text = await fs.readFile(target, 'utf8');
          return {
            content: [{ type: 'text', text }],
          };
        }

        case 'search_notes': {
          if (!args || typeof args.pattern !== 'string') {
            throw new McpError(
              ErrorCode.InvalidParams,
              'pattern is required for search_notes'
            );
          }
          let re;
          try {
            re = new RegExp(args.pattern, 'i');
          } catch {
            re = new RegExp(args.pattern.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i');
          }

          let notes = await listNoteFiles();
          if (typeof args.note === 'string' && args.note.trim()) {
            const { fileName } = resolveNotePath(args.note);
            notes = notes.filter(
              (n) => n.name.toLowerCase() === fileName.toLowerCase()
            );
          }

          const results = [];
          for (const note of notes) {
            const text = await fs.readFile(note.path, 'utf8');
            const lines = text.split(/\r?\n/);
            for (let i = 0; i < lines.length; i++) {
              if (re.test(lines[i])) {
                results.push(`${note.name}:${i + 1}: ${lines[i].trim()}`);
              }
            }
          }

          return {
            content: [
              {
                type: 'text',
                text: results.length
                  ? results.join('\n')
                  : 'No matches found.',
              },
            ],
          };
        }

        case 'check_architecture_rules': {
          const includeOk = args?.includeOk === true;
          const report = await architectureReport(includeOk);
          return {
            content: [{ type: 'text', text: report }],
          };
        }

        case 'check_version_consistency': {
          const report = await versionReport();
          return {
            content: [{ type: 'text', text: report }],
          };
        }

        case 'check_localization_completeness': {
          const report = await localizationReport();
          return {
            content: [{ type: 'text', text: report }],
          };
        }

        case 'check_git_security': {
          const report = await gitSecurityReport();
          return {
            content: [{ type: 'text', text: report }],
          };
        }

        case 'check_xaml_hardcoded_danish': {
          const report = await xamlDanishReport();
          return {
            content: [{ type: 'text', text: report }],
          };
        }

        case 'run_all_checks': {
          const report = await runAllChecks();
          return {
            content: [{ type: 'text', text: report }],
          };
        }

        case 'append_to_note': {
          if (!isValidAppendArgs(args)) {
            throw new McpError(
              ErrorCode.InvalidParams,
              'append_to_note requires valid note, heading and content arguments'
            );
          }
          const { target, fileName } = resolveNotePath(args.note);
          if (!(await fileExists(target))) {
            throw new McpError(
              ErrorCode.InvalidParams,
              `Note not found: ${fileName} (looked at ${target})`
            );
          }
          let text = await fs.readFile(target, 'utf8');
          text = await appendToSection(text, args.heading, args.content);
          await fs.writeFile(target, text, 'utf8');
          return {
            content: [
              {
                type: 'text',
                text: `Appended content under heading "${args.heading}" in ${fileName}.`,
              },
            ],
          };
        }

        case 'replace_in_note': {
          if (!isValidReplaceArgs(args)) {
            throw new McpError(
              ErrorCode.InvalidParams,
              'replace_in_note requires valid note, find and content arguments'
            );
          }
          const { target, fileName } = resolveNotePath(args.note);
          if (!(await fileExists(target))) {
            throw new McpError(
              ErrorCode.InvalidParams,
              `Note not found: ${fileName} (looked at ${target})`
            );
          }
          let text = await fs.readFile(target, 'utf8');
          const index = text.indexOf(args.find);
          if (index === -1) {
            throw new McpError(
              ErrorCode.InvalidParams,
              `Text to replace was not found in ${fileName}.`
            );
          }
          text =
            text.slice(0, index) +
            args.content +
            text.slice(index + args.find.length);
          await fs.writeFile(target, text, 'utf8');
          return {
            content: [
              {
                type: 'text',
                text: `Replaced text in ${fileName}.`,
              },
            ],
          };
        }

        case 'update_note': {
          console.error('[update_note] args received:', JSON.stringify(args));
          if (!isValidUpdateArgs(args)) {
            throw new McpError(
              ErrorCode.InvalidParams,
              buildUpdateArgsError(args)
            );
          }
          const { target, fileName } = resolveNotePath(args.note);
          if (!(await fileExists(target))) {
            throw new McpError(
              ErrorCode.InvalidParams,
              `Note not found: ${fileName} (looked at ${target})`
            );
          }

          let text = await fs.readFile(target, 'utf8');
          let summary = '';

          if (args.operation === 'append') {
            if (typeof args.heading !== 'string' || !args.heading.trim()) {
              throw new McpError(
                ErrorCode.InvalidParams,
                'heading is required for append operation'
              );
            }
            text = await appendToSection(text, args.heading, args.content);
            summary = `Appended content under heading "${args.heading}" in ${fileName}.`;
          } else if (args.operation === 'replace') {
            if (typeof args.find !== 'string' || !args.find) {
              throw new McpError(
                ErrorCode.InvalidParams,
                'find is required for replace operation'
              );
            }
            const index = text.indexOf(args.find);
            if (index === -1) {
              throw new McpError(
                ErrorCode.InvalidParams,
                `Text to replace was not found in ${fileName}.`
              );
            }
            text =
              text.slice(0, index) +
              args.content +
              text.slice(index + args.find.length);
            summary = `Replaced text in ${fileName}.`;
          }

          await fs.writeFile(target, text, 'utf8');
          return {
            content: [{ type: 'text', text: summary }],
          };
        }

        default:
          throw new McpError(
            ErrorCode.MethodNotFound,
            `Unknown tool: ${name}`
          );
      }
    });
  }

  async run() {
    const transport = new StdioServerTransport();
    await this.server.connect(transport);
    console.error('IconGrid notes MCP server running on stdio');
  }
}

const server = new NotesServer();
server.run().catch(console.error);