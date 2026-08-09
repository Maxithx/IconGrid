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
            if (_entries.Any(e => string.Equals(NormalizePath(e.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase)))
                return false;

            var displayName = Path.GetFileNameWithoutExtension(executablePath);
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
        /// </summary>
        public void SetResolution(string executablePath, string? resolution)
        {
            var normalizedPath = NormalizePath(executablePath);
            var entry = _entries.FirstOrDefault(e =>
                string.Equals(NormalizePath(e.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return;

            entry.GameResolution = resolution;
            Save();
        }

        /// <summary>
        /// Looks up a saved resolution for an executable path.
        /// Returns null if not found or no resolution set.
        /// </summary>
        public string? GetResolution(string executablePath)
        {
            var normalizedPath = NormalizePath(executablePath);
            var entry = _entries.FirstOrDefault(e =>
                string.Equals(NormalizePath(e.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
            return entry?.GameResolution;
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