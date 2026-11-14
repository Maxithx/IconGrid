using System;
using System.Collections.Generic;
using IconGrid.Helpers.Launcher;
using IconGrid.Helpers.Skin;
using IconGrid.Models;

namespace IconGrid.ViewModels
{
    public partial class MainViewModel
    {
        /// <summary>
        /// Standard corner/edge placement for the gaming overlay, chosen by the user
        /// ("TopLeft".."BottomRight", or "Custom"). The overlay window snaps to this
        /// preset whenever it opens or the display resolution changes — but the user
        /// can still drag the overlay anywhere afterwards (Custom stores that position).
        /// </summary>
        public string GamingOverlayPositionPreset
        {
            get => _gamingOverlayPositionPreset;
            set
            {
                var clamped = string.IsNullOrWhiteSpace(value) ? "TopRight" : value;
                if (SetField(ref _gamingOverlayPositionPreset, clamped))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the clock block in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowClock
        {
            get => _gamingOverlayShowClock;
            set
            {
                if (SetField(ref _gamingOverlayShowClock, value, nameof(GamingOverlayShowClock)))
                {
                    OnPropertyChanged(nameof(GamingOverlayDividerClockNetworkVisible));
                    OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the ping dot + network status block in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowNetwork
        {
            get => _gamingOverlayShowNetwork;
            set
            {
                if (SetField(ref _gamingOverlayShowNetwork, value, nameof(GamingOverlayShowNetwork)))
                {
                    OnPropertyChanged(nameof(GamingOverlayDividerClockNetworkVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerNetworkCpuVisible));
                    OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the CPU block (temperature + usage bar) in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowCpu
        {
            get => _gamingOverlayShowCpu;
            set
            {
                if (SetField(ref _gamingOverlayShowCpu, value, nameof(GamingOverlayShowCpu)))
                {
                    OnPropertyChanged(nameof(GamingOverlayDividerNetworkCpuVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerCpuGpuVisible));
                    OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the GPU block (temperature + usage bar) in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowGpu
        {
            get => _gamingOverlayShowGpu;
            set
            {
                if (SetField(ref _gamingOverlayShowGpu, value, nameof(GamingOverlayShowGpu)))
                {
                    OnPropertyChanged(nameof(GamingOverlayGpuClockVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerCpuGpuVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerGpuFpsVisible));
                    OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the FPS block in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowFps
        {
            get => _gamingOverlayShowFps;
            set
            {
                if (SetField(ref _gamingOverlayShowFps, value, nameof(GamingOverlayShowFps)))
                {
                    OnPropertyChanged(nameof(GamingOverlayFrameTimeVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerGpuFpsVisible));
                    OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the GPU core clock (MHz/GHz) next to the GPU temperature block.
        /// Persisted in config. Only rendered while the GPU block itself is visible.
        /// </summary>
        public bool GamingOverlayShowGpuClock
        {
            get => _gamingOverlayShowGpuClock;
            set
            {
                if (SetField(ref _gamingOverlayShowGpuClock, value, nameof(GamingOverlayShowGpuClock)))
                {
                    OnPropertyChanged(nameof(GamingOverlayGpuClockVisible));
                    OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the frame time (1000 / FPS, in ms) next to the FPS value.
        /// Persisted in config. Only rendered while the FPS block itself is visible.
        /// </summary>
        public bool GamingOverlayShowFrameTime
        {
            get => _gamingOverlayShowFrameTime;
            set
            {
                if (SetField(ref _gamingOverlayShowFrameTime, value, nameof(GamingOverlayShowFrameTime)))
                {
                    OnPropertyChanged(nameof(GamingOverlayFrameTimeVisible));
                    OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>GPU clock is part of the GPU block, so the block must be visible too.</summary>
        public bool GamingOverlayGpuClockVisible => _gamingOverlayShowGpu && _gamingOverlayShowGpuClock;

        /// <summary>Frame time is part of the FPS block, so the block must be visible too.</summary>
        public bool GamingOverlayFrameTimeVisible => _gamingOverlayShowFps && _gamingOverlayShowFrameTime;

        // Dividers are only shown BETWEEN two visible blocks, so hiding a block never leaves
        // a dangling "|" at the start/end of the overlay row.
        public bool GamingOverlayDividerClockNetworkVisible => _gamingOverlayShowClock && _gamingOverlayShowNetwork;

        public bool GamingOverlayDividerNetworkCpuVisible => _gamingOverlayShowNetwork && _gamingOverlayShowCpu;

        public bool GamingOverlayDividerCpuGpuVisible => _gamingOverlayShowCpu && _gamingOverlayShowGpu;

        public bool GamingOverlayDividerGpuFpsVisible => _gamingOverlayShowGpu && _gamingOverlayShowFps;

        /// <summary>
        /// Active gaming overlay skin (colours + fonts only — never layout). Bindings inside
        /// GamingOverlayBar read straight from this instance, so editing it updates the overlay and
        /// the settings preview live.
        /// </summary>
        public GamingOverlaySkin GamingOverlaySkin => _gamingOverlaySkin;

        /// <summary>
        /// Applies another skin (preset or imported) onto the active instance and persists it.
        /// Copying into the existing instance keeps all nested bindings alive.
        /// </summary>
        public void ApplyGamingOverlaySkin(GamingOverlaySkin skin, bool save)
        {
            if (skin == null)
            {
                return;
            }

            _gamingOverlaySkin.CopyFrom(skin);

            if (save)
            {
                SaveSettingsToConfig();
            }

            OnPropertyChanged(nameof(GamingOverlaySkin));
        }

        /// <summary>
        /// Persists the current skin after an edit in the Skin Studio page.
        /// </summary>
        public void SaveGamingOverlaySkin() => SaveSettingsToConfig();

        /// <summary>
        /// Data-driven overlay blueprint: paint layers + the modular block layout. The overlay row
        /// renders from this list, so blocks can be reordered/added without touching XAML.
        /// </summary>
        public OverlaySkinBlueprint GamingOverlayBlueprint => _gamingOverlayBlueprint;

        /// <summary>JSON of the active blueprint (used by the Skin Studio editor).</summary>
        public string GamingOverlayBlueprintJson => _gamingOverlayBlueprint.ToJson();

        /// <summary>
        /// Replaces the active blueprint. Returns false when the JSON cannot be parsed, so the
        /// editor can show a validation message instead of silently resetting the overlay.
        /// </summary>
        public bool ApplyGamingOverlayBlueprintJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            OverlaySkinBlueprint parsed;
            try
            {
                parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<OverlaySkinBlueprint>(json) ?? new OverlaySkinBlueprint();
            }
            catch
            {
                return false;
            }

            if (parsed.Layout == null || parsed.Layout.Count == 0)
            {
                return false;
            }

            _gamingOverlayBlueprint = parsed;
            OnPropertyChanged(nameof(GamingOverlayBlueprint));
            OnPropertyChanged(nameof(GamingOverlayBlueprintJson));
            OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
            SaveSettingsToConfig();
            return true;
        }

        /// <summary>Restores the built-in classic blueprint (module order of the standard UI).</summary>
        public void ResetGamingOverlayBlueprint()
        {
            _gamingOverlayBlueprint = OverlayBlueprintDefaults.CreateClassic();
            OnPropertyChanged(nameof(GamingOverlayBlueprint));
            OnPropertyChanged(nameof(GamingOverlayBlueprintJson));
            OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
            SaveSettingsToConfig();
        }

        // ---------- Module builder operations (Skin Studio) ----------

        /// <summary>Adds a module of the given type at the end of the row.</summary>
        public void AddGamingOverlayModule(string blockType, string? id = null)
        {
            if (string.IsNullOrWhiteSpace(blockType))
            {
                return;
            }

            var props = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                props["id"] = id;
            }

            // Sensible defaults so a freshly added module renders meaningfully.
            switch (blockType)
            {
                case "ping":
                    props["label"] = "Net";
                    props["showDot"] = "true";
                    break;
                case "gauge":
                    props["label"] = string.Equals(id, "gpu", StringComparison.OrdinalIgnoreCase) ? "GPU" : "CPU";
                    props["hasGrid"] = "true";
                    break;
                case "usage":
                    props["id"] = string.Equals(id, "gpu", StringComparison.OrdinalIgnoreCase) ? "gpu" : "cpu";
                    props["label"] = string.Equals(id, "gpu", StringComparison.OrdinalIgnoreCase) ? "GPU" : "CPU";
                    break;
                case "temp":
                    props["id"] = string.Equals(id, "gpu", StringComparison.OrdinalIgnoreCase) ? "gpu" : "cpu";
                    props["label"] = string.Equals(id, "gpu", StringComparison.OrdinalIgnoreCase) ? "GPU" : "CPU";
                    break;
                case "stat" when string.Equals(id, "frameTime", StringComparison.OrdinalIgnoreCase):
                    props["data"] = "frameTime";
                    props["label"] = "FT";
                    break;
                case "stat" when string.Equals(id, "fps", StringComparison.OrdinalIgnoreCase):
                    props["data"] = "fps";
                    props["label"] = "FPS";
                    props["highlight"] = "true";
                    break;
                case "stat":
                    props["data"] = "gpuClock";
                    break;
            }

            CommitBlueprintLayout(blocks => blocks.Add(new OverlayBlock { Block = blockType, Props = props }));
        }

        public void RemoveGamingOverlayModule(OverlayBlock? block)
        {
            if (block == null)
            {
                return;
            }

            CommitBlueprintLayout(blocks => blocks.Remove(block));
        }

        /// <summary>Moves a module one step left (delta -1) or right (delta +1).</summary>
        public void MoveGamingOverlayModule(OverlayBlock? block, int delta)
        {
            if (block == null || delta == 0)
            {
                return;
            }

            CommitBlueprintLayout(blocks =>
            {
                var index = blocks.IndexOf(block);
                var target = index + delta;
                if (index < 0 || target < 0 || target >= blocks.Count)
                {
                    return;
                }

                blocks.RemoveAt(index);
                blocks.Insert(target, block);
            });
        }

        /// <summary>Shows/hides a module without deleting it (props["hidden"]).</summary>
        public void ToggleGamingOverlayModuleHidden(OverlayBlock? block)
        {
            if (block == null)
            {
                return;
            }

            CommitBlueprintLayout(_ =>
            {
                block.Props ??= new Dictionary<string, string>();
                block.Props["hidden"] = block.IsVisible ? "true" : "false";
            });
        }

        /// <summary>
        /// Sets one property on a module (used by the canvas property panel). Empty values remove it.
        /// </summary>
        public void SetGamingOverlayModuleProperty(OverlayBlock? block, string key, string? value)
        {
            if (block == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            CommitBlueprintLayout(_ =>
            {
                block.Props ??= new Dictionary<string, string>();

                if (string.IsNullOrWhiteSpace(value))
                {
                    block.Props.Remove(key);
                }
                else
                {
                    block.Props[key] = value;
                }
            });
        }

        /// <summary>
        /// Layer list for the Skin Studio panel: reverse order so the last (topmost) module is at
        /// the top, like Photoshop/Figma. The items are the same <see cref="OverlayBlock"/> objects,
        /// so selection and editing still target the real blueprint modules.
        /// </summary>
        public IReadOnlyList<OverlayBlock> GamingOverlayLayerItems
        {
            get
            {
                var items = new List<OverlayBlock>(_gamingOverlayBlueprint.Layout);
                items.Reverse();

                // The settings/close buttons are styled like modules, so they belong in the layer
                // list too — pinned at the bottom because they sit at the right edge of the bar.
                items.AddRange(_gamingOverlayBlueprint.Chrome);

                return items;
            }
        }

        /// <summary>Blueprint chrome block for the overlay settings (cog) button.</summary>
        public OverlayBlock? GamingOverlaySettingsButton =>
            _gamingOverlayBlueprint.Chrome.FirstOrDefault(block =>
                string.Equals(block.Block, "settings", StringComparison.OrdinalIgnoreCase));

        /// <summary>Blueprint chrome block for the overlay close button.</summary>
        public OverlayBlock? GamingOverlayCloseButton =>
            _gamingOverlayBlueprint.Chrome.FirstOrDefault(block =>
                string.Equals(block.Block, "close", StringComparison.OrdinalIgnoreCase));

        /// <summary>Sets the opacity of one module (0..1, stored in props["opacity"]).</summary>
        public void SetGamingOverlayModuleOpacity(OverlayBlock? block, double opacity)
        {
            if (block == null)
            {
                return;
            }

            var clamped = Math.Max(0.0, Math.Min(1.0, opacity));
            SetGamingOverlayModuleProperty(block, "opacity", clamped.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Applies a code snippet to one module. A snippet is a JSON object of property patches
        /// (for example {"meter":"ring","showPercent":"true"}), so it can only write the same
        /// values as the properties panel — it never executes code. The text is kept on the block
        /// so the Skin Studio can show and reset it later.
        /// </summary>
        public bool TryApplyGamingOverlaySnippet(OverlayBlock? block, string? json, out string error)
        {
            error = string.Empty;

            if (block == null)
            {
                error = "no-module";
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "empty";
                return false;
            }

            Newtonsoft.Json.Linq.JObject patch;
            try
            {
                patch = Newtonsoft.Json.Linq.JObject.Parse(json);
            }
            catch
            {
                error = "invalid";
                return false;
            }

            if (patch.Count == 0)
            {
                error = "empty";
                return false;
            }

            var applied = new Dictionary<string, string>();
            foreach (var property in patch.Properties())
            {
                var value = property.Value is Newtonsoft.Json.Linq.JValue scalar
                    ? scalar.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : property.Value.ToString(Newtonsoft.Json.Formatting.None);
                applied[property.Name] = value ?? string.Empty;
            }

            CommitBlueprintLayout(_ =>
            {
                block.Props ??= new Dictionary<string, string>();
                foreach (var pair in applied)
                {
                    if (string.IsNullOrWhiteSpace(pair.Value))
                    {
                        block.Props.Remove(pair.Key);
                    }
                    else
                    {
                        block.Props[pair.Key] = pair.Value;
                    }
                }

                block.Snippet = json.Trim();
            });

            return true;
        }

        /// <summary>
        /// Undoes the last snippet on a module by removing exactly the properties it had set
        /// (read back from the stored snippet text) and clearing the snippet itself.
        /// </summary>
        public void ResetGamingOverlaySnippet(OverlayBlock? block)
        {
            if (block == null)
            {
                return;
            }

            var keys = new List<string>();
            if (!string.IsNullOrWhiteSpace(block.Snippet))
            {
                try
                {
                    keys.AddRange(Newtonsoft.Json.Linq.JObject.Parse(block.Snippet).Properties().Select(p => p.Name));
                }
                catch
                {
                    // Unparsable stored snippet: just drop the text.
                }
            }

            CommitBlueprintLayout(_ =>
            {
                if (block.Props != null)
                {
                    foreach (var key in keys)
                    {
                        block.Props.Remove(key);
                    }
                }

                block.Snippet = null;
            });
        }

        private void CommitBlueprintLayout(Action<List<OverlayBlock>> mutate)
        {
            // Work on a copy and assign a NEW list: the ItemsControl only refreshes its item
            // containers when the collection instance (or a collection change) is observed.
            var blocks = new List<OverlayBlock>(_gamingOverlayBlueprint.Layout);
            mutate(blocks);
            _gamingOverlayBlueprint.Layout = blocks;

            OnPropertyChanged(nameof(GamingOverlayBlueprint));
            OnPropertyChanged(nameof(GamingOverlayBlueprintJson));
            OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
            OnPropertyChanged(nameof(GamingOverlayLayerItems));
            OnPropertyChanged(nameof(GamingOverlaySettingsButton));
            OnPropertyChanged(nameof(GamingOverlayCloseButton));
            SaveSettingsToConfig();
        }

        /// <summary>
        /// Modules the overlay should render: blueprint order, minus blocks whose data source is
        /// switched off in the "Overlay content" card, with dangling dividers removed.
        /// </summary>
        public IReadOnlyList<OverlayBlock> GamingOverlayLayoutBlocks
        {
            get
            {
                var blocks = new List<OverlayBlock>();

                foreach (var block in _gamingOverlayBlueprint.Layout)
                {
                    if (block == null || !block.IsVisible)
                    {
                        continue;
                    }

                    if (!IsBlockVisibleBySettings(block))
                    {
                        continue;
                    }

                    // Never render two dividers in a row, and never start with one.
                    if (block.Block == "divider" && (blocks.Count == 0 || blocks[blocks.Count - 1].Block == "divider"))
                    {
                        continue;
                    }

                    blocks.Add(block);
                }

                while (blocks.Count > 0 && blocks[blocks.Count - 1].Block == "divider")
                {
                    blocks.RemoveAt(blocks.Count - 1);
                }

                return blocks;
            }
        }

        private bool IsBlockVisibleBySettings(OverlayBlock block)
        {
            // Only module types the renderer has templates for are rendered. Unknown types are
            // skipped instead of being shown as "IconGrid.Models.OverlayBlock".
            if (block.Block != "clock" && block.Block != "ping" && block.Block != "gauge" &&
                block.Block != "stat" && block.Block != "divider")
            {
                return false;
            }

            return block.Block switch
            {
                "clock" => _gamingOverlayShowClock,
                "ping" => _gamingOverlayShowNetwork,
                "gauge" => block.Id == "gpu" ? _gamingOverlayShowGpu : _gamingOverlayShowCpu,
                "stat" => block.DataKey switch
                {
                    "gpuClock" => GamingOverlayGpuClockVisible,
                    "frameTime" => GamingOverlayFrameTimeVisible,
                    "fps" => _gamingOverlayShowFps,
                    _ => true
                },
                _ => true
            };
        }

        /// <summary>
        /// Launcher behavior while a game is running: 0=None, 1=AutoHide, 2=MinimizeToTaskbar.
        /// Persisted in config.
        /// </summary>
        public int GameLauncherAutoBehavior
        {
            get => _gameLauncherAutoBehavior;
            set
            {
                var clamped = Math.Max(0, Math.Min(2, value));
                if (SetField(ref _gameLauncherAutoBehavior, clamped))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Automatically show the gaming overlay when a game starts (positioned at the
        /// chosen "Default position"). Persisted in config.
        /// </summary>
        public bool AutoShowGamingOverlayOnGameStart
        {
            get => _autoShowGamingOverlayOnGameStart;
            set
            {
                if (SetField(ref _autoShowGamingOverlayOnGameStart, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Automatically close the gaming overlay when the game exits. Persisted in config.
        /// </summary>
        public bool AutoCloseGamingOverlayOnGameEnd
        {
            get => _autoCloseGamingOverlayOnGameEnd;
            set
            {
                if (SetField(ref _autoCloseGamingOverlayOnGameEnd, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Restore the main launcher (if it was hidden/minimized for a game) when the
        /// gaming overlay is closed — manually or automatically. Persisted in config.
        /// </summary>
        public bool RestoreLauncherAfterOverlayClosed
        {
            get => _restoreLauncherAfterOverlayClosed;
            set
            {
                if (SetField(ref _restoreLauncherAfterOverlayClosed, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Returns the default overlay scale for a resolution like "3840x2160".
        /// Falls back to the built-in default (100% for every resolution — the scale is
        /// the real physical size; the 1440p=135% legacy default only applied when the
        /// overlay window still compensated for resolution) when the user has not
        /// overridden it.
        /// </summary>
        public double GetGamingOverlayScaleForResolution(string? resolution)
        {
            var key = NormalizeResolutionKey(resolution);
            if (key != null && _gamingOverlayResolutionScales.TryGetValue(key, out var saved))
            {
                return Math.Max(1.0, Math.Min(1.5, saved));
            }

            return GetBuiltInOverlayScaleDefault(key);
        }

        /// <summary>
        /// Stores a user-chosen default overlay scale for a resolution like "3840x2160".
        /// </summary>
        public void SetGamingOverlayScaleForResolution(string? resolution, double scale)
        {
            var key = NormalizeResolutionKey(resolution);
            if (key == null)
                return;

            var clamped = Math.Max(1.0, Math.Min(1.5, scale));
            _gamingOverlayResolutionScales[key] = clamped;
            SaveSettingsToConfig();
        }

        /// <summary>
        /// Removes the user override for a resolution, falling back to the built-in default.
        /// </summary>
        public void RemoveGamingOverlayScaleForResolution(string? resolution)
        {
            var key = NormalizeResolutionKey(resolution);
            if (key == null)
                return;

            if (_gamingOverlayResolutionScales.Remove(key))
            {
                SaveSettingsToConfig();
            }
        }

        /// <summary>
        /// Copies of the per-resolution scales for settings UI display.
        /// </summary>
        public IReadOnlyDictionary<string, double> GamingOverlayResolutionScalesSnapshot =>
            new Dictionary<string, double>(_gamingOverlayResolutionScales);

        private static string? NormalizeResolutionKey(string? resolution)
        {
            var parsed = DisplayResolutionService.ParseResolution(resolution);
            return parsed.HasValue ? $"{parsed.Value.Width}x{parsed.Value.Height}" : null;
        }

        private static double GetBuiltInOverlayScaleDefault(string? key)
        {
            // The overlay scale is the REAL physical size: 100% is the design size
            // (720x44) on every resolution. There is no resolution compensation in
            // the overlay window anymore, so every resolution gets the same default
            // (100%). Users can override per resolution via the settings page or the
            // overlay scale slider.
            _ = key;
            return 1.0;
        }
    }
}