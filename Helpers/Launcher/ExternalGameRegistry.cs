using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IconGrid.Helpers.Settings;
using IconGrid.Models;

namespace IconGrid.Helpers.Launcher
{
    /// <summary>
    /// Stores and loads "external games" — games detected via foreground/FPS
    /// detection that are NOT already in the user's IconGrid shortcut list.
    /// 
    /// This allows GameResolutionPage to show and configure resolution switching
    /// for games launched from Battle.net, Steam, Ubisoft Connect, Xbox, etc.
    /// without the user having to manually create a shortcut.
    /// </summary>
    public sealed class ExternalGameRegistry
    {
        private const string ExternalCategory = "External";
        private readonly ConfigManager _configManager;
        private readonly string _registryPath;
        private List<ExternalGameEntry> _entries = new();

        public ExternalGameRegistry()
        {
            _configManager = new ConfigManager();
            _registryPath = Path.Combine(_configManager.BaseDirectory, "external-games.json");
            Load();
        }

        /// <summary>
        /// All known external games (from external-games.json).
        /// </summary>
        public IReadOnlyList<ExternalGameEntry> Entries => _entries;

        /// <summary>
        /// Returns LauncherItem representations of external games so
        /// GameResolutionPage can bind to them exactly like normal shortcuts.
        /// </summary>
        public List<LauncherItem> GetLauncherItems()
        {
            return _entries.Select(e => e.ToLauncherItem()).ToList();
        }

        /// <summary>
        /// Tries to auto-register a process as an external game.
        /// If the executable path is already tracked (by exe path), it is skipped.
        /// Returns true if a new entry was added.
        /// </summary>
        public bool TryRegister(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return false;

            var normalizedPath = NormalizePath(executablePath);

            // Deduplicate by normalized path.
            if (_entries.Any(e => string.Equals(NormalizePath(e.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase)))
                return false;

            var displayName = Path.GetFileNameWithoutExtension(executablePath);

            // Also deduplicate by display name — a bare process name (e.g. "cod22-cod.exe")
            // may conflict with a previously registered corrupted CWD-relative path
            // (e.g. "C:\IconGrid\cod22-cod.exe") that resolves to the same display name.
            if (_entries.Any(e => string.Equals(e.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)))
                return false;

            var entry = new ExternalGameEntry
            {
                ExecutablePath = normalizedPath,
                DisplayName = displayName,
                FirstDetectedAtUtc = DateTime.UtcNow
            };
            _entries.Add(entry);
            Save();
            return true;
        }

        /// <summary>
        /// Saves a resolution for an external game entry and persists.
        /// Tries exact path match first, then falls back to display name.
        /// This handles the case where old entries have a different key format
        /// (e.g. bare process name vs CWD-relative path) but share the same
        /// DisplayName (e.g. "cod22-cod").
        /// </summary>
        public void SetResolution(string executablePath, string? resolution)
        {
            var entry = FindEntry(executablePath);
            if (entry == null)
                return;

            entry.GameResolution = resolution;
            Save();
        }

        /// <summary>
        /// Looks up a saved resolution for an executable path.
        /// Tries exact path match first, then falls back to display name.
        /// Returns null if not found or no resolution set.
        /// </summary>
        public string? GetResolution(string executablePath)
        {
            var entry = FindEntry(executablePath);
            return entry?.GameResolution;
        }

        /// <summary>
        /// Finds an entry by exact path match first, then by display name fallback.
        /// When multiple entries share the same DisplayName (e.g. old corrupted
        /// CWD-relative path vs new bare process name), prefers the entry that
        /// actually has a saved GameResolution.
        /// </summary>
        private ExternalGameEntry? FindEntry(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return null;

            var normalizedPath = NormalizePath(executablePath);
            var displayName = Path.GetFileNameWithoutExtension(executablePath);

            // Collect all entries matching by path OR display name.
            var candidates = _entries.Where(e =>
                string.Equals(NormalizePath(e.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(displayName) &&
                 string.Equals(e.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (candidates.Count == 0)
                return null;

            // Prefer an entry that has a saved resolution (likely the one the
            // user configured), otherwise return the first match.
            return candidates.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.GameResolution))
                   ?? candidates[0];
        }

        /// <summary>
        /// Checks whether a given executable path is registered.
        /// </summary>
        public bool IsRegistered(string executablePath)
        {
            var normalizedPath = NormalizePath(executablePath);
            return _entries.Any(e =>
                string.Equals(NormalizePath(e.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Removes an external game entry by its executable path.
        /// Persists the change to external-games.json immediately.
        /// </summary>
        public void Remove(string executablePath)
        {
            var normalizedPath = NormalizePath(executablePath);
            _entries.RemoveAll(e =>
                string.Equals(NormalizePath(e.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_registryPath))
                {
                    _entries = new List<ExternalGameEntry>();
                    return;
                }

                var json = File.ReadAllText(_registryPath);
                var entries = JsonSerializer.Deserialize<List<ExternalGameEntry>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                _entries = entries ?? new List<ExternalGameEntry>();
            }
            catch
            {
                _entries = new List<ExternalGameEntry>();
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(_configManager.BaseDirectory);
                var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
                File.WriteAllText(_registryPath, json);
            }
            catch
            {
                // non-critical persistence failure
            }
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            // If the path has no directory separator, it's a bare process name
            // (e.g. "cod22-cod.exe") used as a fallback identifier for UWP/GamePass
            // games where MainModule.FileName is unavailable. Do NOT expand it
            // via Path.GetFullPath — that would produce a spurious CWD-relative
            // path that breaks persistence and cross-session lookups.
            var hasDirectory = path.Contains(Path.DirectorySeparatorChar) ||
                               path.Contains(Path.AltDirectorySeparatorChar);
            if (!hasDirectory)
                return path;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }
    }

    /// <summary>
    /// A single external game entry stored in external-games.json.
    /// </summary>
    public sealed class ExternalGameEntry
    {
        public string ExecutablePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? GameResolution { get; set; }
        public DateTime FirstDetectedAtUtc { get; set; }

        /// <summary>
        /// Converts this entry to a LauncherItem so GameResolutionPage
        /// can bind it using the same DataTemplate as normal shortcuts.
        /// </summary>
        public LauncherItem ToLauncherItem()
        {
            return new LauncherItem
            {
                DisplayName = DisplayName,
                Path = ExecutablePath,
                Category = "External",
                GameResolution = GameResolution
            };
        }
    }
}