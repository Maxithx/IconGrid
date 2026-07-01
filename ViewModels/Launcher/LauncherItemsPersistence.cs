using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using IconGrid.Models;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherItemsPersistence
    {
        private readonly string _dataFolder;
        private readonly string _itemsFilePath;
        private readonly string _legacyItemsFilePath;

        public LauncherItemsPersistence(string dataFolder, string legacyItemsFilePath)
        {
            _dataFolder = dataFolder;
            _itemsFilePath = Path.Combine(dataFolder, "items.json");
            _legacyItemsFilePath = legacyItemsFilePath;
        }

        public void Save(IEnumerable<LauncherItem> items)
        {
            try
            {
                Directory.CreateDirectory(_dataFolder);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(items.ToList(), options);
                File.WriteAllText(_itemsFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to save items: " + ex);
            }
        }

        public List<LauncherItem> Load()
        {
            try
            {
                if (!File.Exists(_itemsFilePath))
                    return new List<LauncherItem>();

                var json = File.ReadAllText(_itemsFilePath);
                return JsonSerializer.Deserialize<List<LauncherItem>>(json) ?? new List<LauncherItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to load items: " + ex);
                return new List<LauncherItem>();
            }
        }

        public void MigrateLegacyIfNeeded()
        {
            try
            {
                if (File.Exists(_itemsFilePath) || !File.Exists(_legacyItemsFilePath))
                    return;

                Directory.CreateDirectory(_dataFolder);
                File.Copy(_legacyItemsFilePath, _itemsFilePath, overwrite: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to migrate legacy items: " + ex);
            }
        }
    }
}
