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

        /// <summary>Stable pseudo blocks for the layer-panel group rows (keyed by group id).</summary>
        private readonly Dictionary<string, OverlayBlock> _groupHeaders = new(StringComparer.Ordinal);

        // ---------- Undo for the layer panel ----------

        private const int MaxUndoSnapshots = 40;
        private readonly List<string> _undoSnapshots = new();
        private bool _isRestoringUndo;

        /// <summary>True when there is at least one layer-panel change to undo.</summary>
        public bool CanUndoGamingOverlayLayout => _undoSnapshots.Count > 0;

        /// <summary>Stores the current blueprint JSON before a change, so it can be undone.</summary>
        private void PushUndoSnapshot()
        {
            if (_isRestoringUndo)
            {
                return;
            }

            _undoSnapshots.Add(_gamingOverlayBlueprint.ToJson());
            if (_undoSnapshots.Count > MaxUndoSnapshots)
            {
                _undoSnapshots.RemoveAt(0);
            }

            OnPropertyChanged(nameof(CanUndoGamingOverlayLayout));
        }

        /// <summary>Reverts the last layer-panel change (add/move/delete/group/eye/...).</summary>
        public bool UndoGamingOverlayLayout()
        {
            if (_undoSnapshots.Count == 0)
            {
                return false;
            }

            var json = _undoSnapshots[^1];
            _undoSnapshots.RemoveAt(_undoSnapshots.Count - 1);

            _isRestoringUndo = true;
            try
            {
                _gamingOverlayBlueprint = OverlaySkinBlueprint.FromJson(json);

                OnPropertyChanged(nameof(GamingOverlayBlueprint));
                OnPropertyChanged(nameof(GamingOverlayBlueprintJson));
                OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
                OnPropertyChanged(nameof(GamingOverlayLayerItems));
                OnPropertyChanged(nameof(GamingOverlayGroups));
                OnPropertyChanged(nameof(GamingOverlaySettingsButton));
                OnPropertyChanged(nameof(GamingOverlayCloseButton));
                OnPropertyChanged(nameof(GamingOverlayBackgroundLayer));
                OnPropertyChanged(nameof(GamingOverlayHasCombinedGauges));
                OnPropertyChanged(nameof(GamingOverlayHasMergeableGauges));
                OnPropertyChanged(nameof(SkinStudioGaugeLayoutActionText));
                SaveSettingsToConfig();
            }
            finally
            {
                _isRestoringUndo = false;
            }

            OnPropertyChanged(nameof(CanUndoGamingOverlayLayout));
            return true;
        }

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
            RaiseCanvasGroupChanged();
            SaveSettingsToConfig();
        }

        /// <summary>
        /// Explicit save from the Skin Studio File menu. Every edit already autosaves, so this
        /// simply flushes the blueprint to disk again — useful as a deliberate "I am done" action.
        /// </summary>
        public void SaveSkinStudioOverlay()
        {
            OnPropertyChanged(nameof(GamingOverlayBlueprintJson));
            SaveSettingsToConfig();
        }

        // ---------- Module builder operations (Skin Studio) ----------

        /// <summary>
        /// Adds a module of the given type at the end of the row, inside <paramref name="groupId"/>.
        /// New layers are never placed in the protected standard group: an explicit request for it
        /// is redirected to the first custom group (a fresh one is created when the user has none).
        /// </summary>
        public OverlayBlock? AddGamingOverlayModule(string blockType, string? id = null, string? groupId = null)
        {
            if (string.IsNullOrWhiteSpace(blockType))
            {
                return null;
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

            props["group"] = ResolveNewModuleGroup(groupId).Id;

            var added = new OverlayBlock { Block = blockType, Props = props };
            CommitBlueprintLayout(blocks => blocks.Add(added));
            return added;
        }

        /// <summary>
        /// Target group for a newly added layer: the requested group unless it is the protected
        /// standard group, otherwise the first custom group, and a brand new one when the user has
        /// no group of their own yet (the standard group stays exactly as shipped).
        /// </summary>
        private OverlayLayerGroup ResolveNewModuleGroup(string? groupId)
        {
            var groups = _gamingOverlayBlueprint.Groups;
            var requested = string.IsNullOrWhiteSpace(groupId)
                ? null
                : groups.FirstOrDefault(group =>
                    string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase));

            if (requested is { IsSystem: false })
            {
                return requested;
            }

            var custom = groups.FirstOrDefault(group => !group.IsSystem);
            return custom ?? AddGamingOverlayGroup();
        }

        public void RemoveGamingOverlayModule(OverlayBlock? block)
        {
            // The standard group is the base of every skin: its layers are never deleted.
            if (block == null || block.IsLocked || IsInSystemGroup(block))
            {
                return;
            }

            CommitBlueprintLayout(blocks => blocks.Remove(block));
        }

        /// <summary>True when a module or chrome block lives in the protected standard group.</summary>
        private bool IsInSystemGroup(OverlayBlock block)
        {
            if (block.IsGroupLayer)
            {
                return block.IsSystemGroup;
            }

            var groups = _gamingOverlayBlueprint.Groups;
            if (groups.Count == 0 || !groups[0].IsSystem)
            {
                return false;
            }

            return string.Equals(GroupOf(block, groups), groups[0].Id, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Moves a module one step left (delta -1) or right (delta +1).</summary>
        public void MoveGamingOverlayModule(OverlayBlock? block, int delta)
        {
            if (block == null || block.IsLocked || delta == 0)
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
                var items = new List<OverlayBlock>();

                // Chrome (action buttons + bar background) is organised like a module: it lives in
                // whichever group it was dropped into.
                var groups = _gamingOverlayBlueprint.Groups;
                var chromeButtons = _gamingOverlayBlueprint.Chrome.Where(block => block.IsButtonLayer).ToList();
                var background = _gamingOverlayBlueprint.Chrome.Where(block => block.IsBackgroundLayer).ToList();

                for (var index = 0; index < groups.Count; index++)
                {
                    var group = groups[index];
                    var header = HeaderFor(group);
                    header.IsGroupHidden = group.IsHidden;
                    header.IsSystemGroupMember = group.IsSystem;
                    items.Add(header);

                    // A collapsed folder shows only its own row (chrome included).
                    if (_collapsedGroups.Contains(group.Id))
                    {
                        continue;
                    }

                    // The overlay chrome behaves like a module for organisation: it lives in the
                    // group it was dropped into (buttons on top, bar background at the bottom).
                    var groupButtons = chromeButtons.Where(chrome => GroupOf(chrome, groups) == group.Id).ToList();
                    var groupBackground = background.Where(chrome => GroupOf(chrome, groups) == group.Id).ToList();

                    foreach (var chrome in groupButtons.Concat(groupBackground))
                    {
                        chrome.IsGroupHidden = group.IsHidden;
                        chrome.IsSystemGroupMember = group.IsSystem;
                    }

                    items.AddRange(groupButtons);

                    var blocks = _gamingOverlayBlueprint.Layout
                        .Where(block => string.Equals(GroupOf(block, groups), group.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var block in blocks)
                    {
                        block.IsGroupHidden = group.IsHidden;
                        block.IsSystemGroupMember = group.IsSystem;
                    }

                    blocks.Reverse();
                    items.AddRange(blocks);
                    items.AddRange(groupBackground);
                }

                return items;
            }
        }

        /// <summary>Layer groups (folders) of the current blueprint.</summary>
        public IReadOnlyList<OverlayLayerGroup> GamingOverlayGroups => _gamingOverlayBlueprint.Groups;

        /// <summary>
        /// Layer group the Skin Studio canvas is designing. Each group is its own overlay bar
        /// (own modules, own width/height), so the canvas only shows the selected group.
        /// </summary>
        private string? _canvasGroupId;

        /// <summary>Group shown on the canvas: the selected row, else the standard group.</summary>
        private OverlayLayerGroup? CanvasGroup
        {
            get
            {
                var groups = _gamingOverlayBlueprint.Groups;
                return groups.FirstOrDefault(group =>
                           string.Equals(group.Id, _canvasGroupId, StringComparison.Ordinal))
                       ?? groups.FirstOrDefault();
            }
        }

        /// <summary>Name shown above the canvas (the group being designed).</summary>
        public string GamingOverlayCanvasTitle => CanvasGroup?.Name ?? string.Empty;

        /// <summary>Id of the group being designed (the preview bar filters chrome by it).</summary>
        public string? GamingOverlayCanvasGroupId => CanvasGroup?.Id;

        /// <summary>
        /// True when a layer (module or chrome) belongs to the given group. Used by the preview bar
        /// so the settings/close buttons only appear in the bar of the group that owns them.
        /// </summary>
        public bool GamingOverlayGroupOwns(OverlayBlock? block, string? groupId)
        {
            if (block == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(groupId))
            {
                return true;
            }

            var groups = _gamingOverlayBlueprint.Groups;
            return string.Equals(GroupOf(block, groups), groupId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Modules the canvas bar renders: only the visible ones of the canvas group.</summary>
        public IReadOnlyList<OverlayBlock> GamingOverlayCanvasBlocks
        {
            get
            {
                var group = CanvasGroup;
                if (group == null)
                {
                    return Array.Empty<OverlayBlock>();
                }

                var groups = _gamingOverlayBlueprint.Groups;
                return _gamingOverlayBlueprint.Layout
                    .Where(block => block.IsVisible &&
                                    string.Equals(GroupOf(block, groups), group.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>Fixed bar width for the canvas group; NaN = grow with the modules.</summary>
        public double GamingOverlayCanvasBarWidth =>
            CanvasGroup is { Width: > 0 } group ? group.Width : double.NaN;

        /// <summary>Fixed bar height for the canvas group; NaN = the skin's bar height.</summary>
        public double GamingOverlayCanvasBarHeight =>
            CanvasGroup is { Height: > 0 } group ? group.Height : double.NaN;

        /// <summary>Points the canvas at one group (called when the layer selection changes).</summary>
        public void SetSkinStudioCanvasGroup(string? groupId)
        {
            if (string.Equals(_canvasGroupId, groupId, StringComparison.Ordinal))
            {
                return;
            }

            _canvasGroupId = groupId;
            RaiseCanvasGroupChanged();
        }

        private void RaiseCanvasGroupChanged()
        {
            OnPropertyChanged(nameof(GamingOverlayCanvasTitle));
            OnPropertyChanged(nameof(GamingOverlayCanvasGroupId));
            OnPropertyChanged(nameof(GamingOverlayCanvasBars));
            OnPropertyChanged(nameof(GamingOverlayCanvasBlocks));
            OnPropertyChanged(nameof(GamingOverlayCanvasBarWidth));
            OnPropertyChanged(nameof(GamingOverlayCanvasBarHeight));
        }

        /// <summary>
        /// All layer groups as independent bars on the canvas: the standard overlay stays visible
        /// while a new group is being built, each bar at its own stored position.
        /// </summary>
        public IReadOnlyList<OverlayCanvasBar> GamingOverlayCanvasBars
        {
            get
            {
                var groups = _gamingOverlayBlueprint.Groups;
                var bars = new List<OverlayCanvasBar>(groups.Count);

                foreach (var group in groups)
                {
                    var blocks = _gamingOverlayBlueprint.Layout
                        .Where(block => block.IsVisible &&
                                        string.Equals(GroupOf(block, groups), group.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var isSelected = string.Equals(group.Id, CanvasGroup?.Id, StringComparison.Ordinal);
                    bars.Add(new OverlayCanvasBar(group, blocks, isSelected));
                }

                return bars;
            }
        }

        /// <summary>
        /// Moves one group's bar on the canvas (called while dragging it). Pass commit = false
        /// during the drag and true once to store the position with an undo snapshot.
        /// </summary>
        public void SetGamingOverlayGroupPosition(string? groupId, double x, double y, bool commit)
        {
            var group = _gamingOverlayBlueprint.Groups.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, groupId, StringComparison.Ordinal));

            if (group == null)
            {
                return;
            }

            var clampedX = Math.Clamp(x, OverlayCanvasBar.MinOffset, 3000);
            var clampedY = Math.Clamp(y, OverlayCanvasBar.MinOffset, 3000);

            if (commit)
            {
                if (Math.Abs(group.X - clampedX) < 0.01 && Math.Abs(group.Y - clampedY) < 0.01)
                {
                    return;
                }

                group.X = clampedX;
                group.Y = clampedY;
                CommitBlueprintLayout(_ => { });
                return;
            }

            group.X = clampedX;
            group.Y = clampedY;
        }

        /// <summary>
        /// Sets the fixed bar size of one group (0 = automatic). The canvas uses it as the base
        /// template you design inside, exactly like the standard overlay's bar.
        /// </summary>
        public void SetGamingOverlayGroupBarSize(string? groupId, double width, double height)
        {
            var group = _gamingOverlayBlueprint.Groups.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, groupId, StringComparison.Ordinal));

            if (group == null)
            {
                return;
            }

            width = width <= 0 ? 0 : Math.Clamp(width, 60, 4000);
            height = height <= 0 ? 0 : Math.Clamp(height, 18, 1200);

            if (Math.Abs(group.Width - width) < 0.01 && Math.Abs(group.Height - height) < 0.01)
            {
                return;
            }

            group.Width = width;
            group.Height = height;
            CommitBlueprintLayout(_ => { });
        }

        /// <summary>True when the first group (the standard overlay) is hidden via its folder eye.</summary>
        public bool GamingOverlayFirstGroupHidden =>
            _gamingOverlayBlueprint.Groups.Count > 0 && _gamingOverlayBlueprint.Groups[0].IsHidden;

        /// <summary>
        /// Stable pseudo block that represents a group row in the layer panel, so selecting and
        /// renaming a group keeps working while the list is rebuilt after every edit.
        /// </summary>
        private OverlayBlock HeaderFor(OverlayLayerGroup group)
        {
            if (!_groupHeaders.TryGetValue(group.Id, out var header))
            {
                header = new OverlayBlock { Block = "group" };
                _groupHeaders[group.Id] = header;
            }

            header.Props = new Dictionary<string, string>
            {
                // "group" is the key OverlayBlock.GroupId reads, so group actions (rename/delete)
                // work for the header row exactly like they do for a module.
                ["group"] = group.Id,
                ["name"] = group.Name,
                ["hidden"] = group.IsHidden ? "true" : "false",
                ["collapsed"] = _collapsedGroups.Contains(group.Id) ? "true" : "false",
                ["locked"] = group.IsLocked ? "true" : "false",
                ["system"] = group.IsSystem ? "true" : "false"
            };

            return header;
        }

        /// <summary>Group folders the user collapsed in the layer panel (not persisted).</summary>
        private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);

        /// <summary>
        /// Chrome layers (settings button, close button, bar background) are real layers that live in
        /// a group, just like modules — but they are single instances in the blueprint because the
        /// overlay has exactly one set of action buttons. The module library offers them so a new
        /// group can own them too; adding one moves it into that group.
        /// </summary>
        public bool AddGamingOverlayChromeModule(string kind, string? groupId)
        {
            var blockName = (kind ?? string.Empty).Trim().ToLowerInvariant();
            if (blockName != "settings" && blockName != "close" && blockName != "background")
            {
                return false;
            }

            var target = ResolveNewModuleGroup(groupId);
            var chrome = _gamingOverlayBlueprint.Chrome;

            var existing = chrome.FirstOrDefault(block =>
                string.Equals(block.Block, blockName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                var groups = _gamingOverlayBlueprint.Groups;
                if (string.Equals(GroupOf(existing, groups), target.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                CommitBlueprintLayout(_ =>
                {
                    existing.Props ??= new Dictionary<string, string>();
                    existing.Props["group"] = target.Id;
                });

                return true;
            }

            var added = new OverlayBlock
            {
                Block = blockName,
                Props = new Dictionary<string, string> { ["group"] = target.Id }
            };

            CommitBlueprintLayout(_ => _gamingOverlayBlueprint.Chrome.Add(added));

            return true;
        }

        /// <summary>Eye on a group folder: hides or shows every module inside it.</summary>
        public void ToggleGamingOverlayGroupHidden(OverlayBlock? header)
        {
            if (header is not { IsGroupLayer: true })
            {
                return;
            }

            var group = _gamingOverlayBlueprint.Groups.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, header.GroupId, StringComparison.Ordinal));

            if (group == null)
            {
                return;
            }

            group.IsHidden = !group.IsHidden;
            CommitBlueprintLayout(_ => { });
        }

        /// <summary>Expands/collapses a group folder in the layer panel.</summary>
        public void ToggleGamingOverlayGroupCollapsed(OverlayBlock? header)
        {
            if (header is not { IsGroupLayer: true } || string.IsNullOrWhiteSpace(header.GroupId))
            {
                return;
            }

            if (!_collapsedGroups.Remove(header.GroupId))
            {
                _collapsedGroups.Add(header.GroupId);
            }

            OnPropertyChanged(nameof(GamingOverlayLayerItems));
        }

        /// <summary>
        /// Toggles the padlock of the selected layer row: a locked module cannot be moved,
        /// regrouped or deleted, and a locked folder cannot be deleted.
        /// </summary>
        public void ToggleGamingOverlayLock(OverlayBlock? row)
        {
            if (row == null)
            {
                return;
            }

            if (row.IsGroupLayer)
            {
                var group = _gamingOverlayBlueprint.Groups.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, row.GroupId, StringComparison.Ordinal));

                if (group == null)
                {
                    return;
                }

                group.IsLocked = !group.IsLocked;
                CommitBlueprintLayout(_ => { });
                return;
            }

            SetGamingOverlayModuleProperty(row, "locked", row.IsLocked ? null : "true");
        }

        /// <summary>Creates an empty layer group (a folder for a new/custom overlay).</summary>
        public OverlayLayerGroup AddGamingOverlayGroup(string? name = null)
        {
            var groups = new List<OverlayLayerGroup>(_gamingOverlayBlueprint.Groups);
            var group = new OverlayLayerGroup
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name)
                    ? $"Group {groups.Count + 1}"
                    : name.Trim(),
                // Stagger new bars so they do not land on top of the standard overlay.
                Y = groups.Count == 0 ? 0 : groups.Max(candidate => candidate.Y) + 70
            };

            groups.Add(group);
            CommitBlueprintLayout(_ => _gamingOverlayBlueprint.Groups = groups);
            OnPropertyChanged(nameof(GamingOverlayGroups));
            return group;
        }

        /// <summary>Renames one layer group.</summary>
        public void RenameGamingOverlayGroup(string? groupId, string? name)
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var group = _gamingOverlayBlueprint.Groups
                .FirstOrDefault(candidate => string.Equals(candidate.Id, groupId, StringComparison.Ordinal));

            if (group == null || string.Equals(group.Name, name.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            group.Name = name.Trim();
            CommitBlueprintLayout(_ => { });
        }

        /// <summary>Group a block belongs to (empty group prop = the first group).</summary>
        private static string GroupOf(OverlayBlock block, IReadOnlyList<OverlayLayerGroup> groups)
        {
            if (!string.IsNullOrWhiteSpace(block.GroupId))
            {
                return block.GroupId;
            }

            return groups.Count > 0 ? groups[0].Id : "main";
        }

        /// <summary>Moves a module into another layer group (empty id = the first group).</summary>
        public void SetGamingOverlayModuleGroup(OverlayBlock? block, string? groupId)
        {
            // A layer of the standard group cannot be moved out of it (that would make it
            // deletable and break the shipped telemetry layout).
            if (block == null || block.IsLocked || block.IsGroupLayer || IsInSystemGroup(block))
            {
                return;
            }

            SetGamingOverlayModuleProperty(block, "group", string.IsNullOrWhiteSpace(groupId) ? null : groupId);
        }

        /// <summary>
        /// Drag &amp; drop inside the layer panel: puts <paramref name="block"/> right above
        /// <paramref name="target"/> (adopting its group when they differ).
        /// The panel lists modules in reverse layout order, so "above in the panel" means
        /// "after the target" in the layout list.
        /// </summary>
        public void MoveGamingOverlayModuleBefore(OverlayBlock? block, OverlayBlock? target, bool placeBelow = false)
        {
            if (block == null || target == null || block == target ||
                block.IsLocked || target.IsLocked ||
                block.IsGroupLayer || target.IsGroupLayer || block.IsChromeButton || target.IsChromeButton)
            {
                return;
            }

            CommitBlueprintLayout(blocks =>
            {
                if (!blocks.Contains(block) || !blocks.Contains(target))
                {
                    return;
                }

                var targetGroup = target.GroupId;
                if (!string.Equals(block.GroupId, targetGroup, StringComparison.OrdinalIgnoreCase))
                {
                    block.Props ??= new Dictionary<string, string>();
                    if (string.IsNullOrWhiteSpace(targetGroup))
                    {
                        block.Props.Remove("group");
                    }
                    else
                    {
                        block.Props["group"] = targetGroup;
                    }
                }

                blocks.Remove(block);
                var targetIndex = blocks.IndexOf(target);

                // The panel lists modules in reverse layout order: "above the target in the panel"
                // means after it in the layout, "below" means before it.
                var insertAt = placeBelow ? targetIndex : targetIndex + 1;
                blocks.Insert(Math.Max(0, Math.Min(insertAt, blocks.Count)), block);
            });
        }

        /// <summary>
        /// Deletes a layer group. Its modules are moved to the first remaining group so no layer is
        /// lost, and the last group can never be removed (every module needs a folder).
        /// </summary>
        public bool RemoveGamingOverlayGroup(string? groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return false;
            }

            var groups = _gamingOverlayBlueprint.Groups;
            if (groups.Count <= 1)
            {
                return false;
            }

            if (groups.Any(candidate =>
                    string.Equals(candidate.Id, groupId, StringComparison.Ordinal) && candidate.IsLocked))
            {
                return false;
            }

            var group = groups.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, groupId, StringComparison.Ordinal));

            // The standard group is the base of every skin: it can never be removed, not even
            // when the user created other groups and moved modules out of it.
            if (group == null || group.IsSystem)
            {
                return false;
            }

            var remaining = groups.Where(candidate => !ReferenceEquals(candidate, group)).ToList();
            var fallbackId = remaining[0].Id;
            var defaultGroupId = groups[0].Id;

            CommitBlueprintLayout(blocks =>
            {
                foreach (var block in blocks)
                {
                    var blockGroup = string.IsNullOrWhiteSpace(block.GroupId) ? defaultGroupId : block.GroupId;
                    if (!string.Equals(blockGroup, groupId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    block.Props ??= new Dictionary<string, string>();
                    block.Props["group"] = fallbackId;
                }

                // The chrome rows are organised like modules, so they move to the fallback group too.
                foreach (var chrome in _gamingOverlayBlueprint.Chrome)
                {
                    if (string.Equals(chrome.GroupId, groupId, StringComparison.OrdinalIgnoreCase))
                    {
                        chrome.Props ??= new Dictionary<string, string>();
                        chrome.Props["group"] = fallbackId;
                    }
                }

                _gamingOverlayBlueprint.Groups = remaining;
            });

            _groupHeaders.Remove(groupId);
            OnPropertyChanged(nameof(GamingOverlayGroups));
            return true;
        }

        /// <summary>Blueprint chrome block for the overlay settings (cog) button.</summary>
        public OverlayBlock? GamingOverlaySettingsButton =>
            _gamingOverlayBlueprint.Chrome.FirstOrDefault(block =>
                string.Equals(block.Block, "settings", StringComparison.OrdinalIgnoreCase));

        /// <summary>Blueprint chrome block for the overlay close button.</summary>
        public OverlayBlock? GamingOverlayCloseButton =>
            _gamingOverlayBlueprint.Chrome.FirstOrDefault(block =>
                string.Equals(block.Block, "close", StringComparison.OrdinalIgnoreCase));

        /// <summary>Blueprint chrome block for the overlay bar background (eye = hide the bar fill).</summary>
        public OverlayBlock? GamingOverlayBackgroundLayer =>
            _gamingOverlayBlueprint.Chrome.FirstOrDefault(block =>
                string.Equals(block.Block, "background", StringComparison.OrdinalIgnoreCase));

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

        /// <summary>
        /// Niveau B: gives one module its own XAML DataTemplate. The markup is validated before it
        /// is stored, so the overlay only ever sees a template that parsed successfully; everything
        /// else is rejected with an error key ("empty", "too-long", "blocked", "invalid").
        /// </summary>
        public bool TryApplyGamingOverlayTemplate(OverlayBlock? block, string? xaml, out string error)
        {
            error = string.Empty;

            if (block == null)
            {
                error = "no-module";
                return false;
            }

            if (!IconGrid.Helpers.Skin.OverlaySnippetTemplateFactory.TryBuild(xaml, out var template, out error) ||
                template == null)
            {
                return false;
            }

            var text = xaml!.Trim();
            CommitBlueprintLayout(_ => block.Template = text);
            return true;
        }

        /// <summary>Removes a module's custom template so the built-in one renders again.</summary>
        public void ResetGamingOverlayTemplate(OverlayBlock? block)
        {
            if (block == null || string.IsNullOrWhiteSpace(block.Template))
            {
                return;
            }

            CommitBlueprintLayout(_ => block.Template = null);
        }

        /// <summary>
        /// Splits every combined "gauge" module into a separate "usage" meter and a "temp" chip,
        /// so the layer list matches the module library (CPU usage / CPU temp as two layers).
        /// Returns the number of modules that were split.
        /// </summary>
        public int SplitGamingOverlayGaugeModules()
        {
            var split = 0;

            CommitBlueprintLayout(blocks =>
            {
                for (var i = 0; i < blocks.Count; i++)
                {
                    var block = blocks[i];
                    if (!string.Equals(block.Block, "gauge", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // The temperature module keeps the same identity (id/label/colour/hidden/
                    // opacity); the meter module keeps everything else and drops the temp toggle.
                    var tempProps = new Dictionary<string, string>(block.Props ?? new Dictionary<string, string>());

                    block.Block = "usage";
                    block.Props ??= new Dictionary<string, string>();
                    block.Props.Remove("showTemp");

                    blocks.Insert(i + 1, new OverlayBlock { Block = "temp", Props = tempProps });
                    i++;
                    split++;
                }
            });

            return split;
        }

        /// <summary>True when the layout still has combined "gauge" (usage + temp) modules.</summary>
        public bool GamingOverlayHasCombinedGauges =>
            _gamingOverlayBlueprint.Layout.Any(block =>
                string.Equals(block.Block, "gauge", StringComparison.OrdinalIgnoreCase));

        /// <summary>True when a usage meter and a temp chip share the same CPU/GPU id.</summary>
        public bool GamingOverlayHasMergeableGauges
        {
            get
            {
                var temps = new HashSet<string>(
                    _gamingOverlayBlueprint.Layout
                        .Where(block => string.Equals(block.Block, "temp", StringComparison.OrdinalIgnoreCase))
                        .Select(block => block.Id.ToLowerInvariant()));

                return _gamingOverlayBlueprint.Layout.Any(block =>
                    string.Equals(block.Block, "usage", StringComparison.OrdinalIgnoreCase) &&
                    temps.Contains(block.Id.ToLowerInvariant()));
            }
        }

        /// <summary>Label for the studio button: it splits combined gauges, or merges split ones.</summary>
        public string SkinStudioGaugeLayoutActionText =>
            GamingOverlayHasCombinedGauges ? SkinStudioSplitGaugesText : SkinStudioMergeGaugesText;

        /// <summary>
        /// Merges a "usage" meter and the matching "temp" chip (same CPU/GPU id) back into one
        /// combined "gauge" module. Returns the number of merged pairs.
        /// </summary>
        public int MergeGamingOverlayGaugeModules()
        {
            var merged = 0;

            CommitBlueprintLayout(blocks =>
            {
                for (var i = 0; i < blocks.Count; i++)
                {
                    var usage = blocks[i];
                    if (!string.Equals(usage.Block, "usage", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var id = usage.Id;
                    var tempIndex = blocks.FindIndex(candidate =>
                        !ReferenceEquals(candidate, usage) &&
                        string.Equals(candidate.Block, "temp", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));

                    if (tempIndex < 0)
                    {
                        continue;
                    }

                    // Temperature identity + meter settings, and the combined module always shows temp.
                    var props = new Dictionary<string, string>(blocks[tempIndex].Props ?? new Dictionary<string, string>());
                    foreach (var pair in usage.Props ?? new Dictionary<string, string>())
                    {
                        props[pair.Key] = pair.Value;
                    }

                    props["showTemp"] = "true";
                    props["id"] = id;

                    usage.Props = props;
                    usage.Block = "gauge";
                    blocks.RemoveAt(tempIndex);

                    if (tempIndex < i)
                    {
                        i--;
                    }

                    merged++;
                }
            });

            return merged;
        }

        private void CommitBlueprintLayout(Action<List<OverlayBlock>> mutate)
        {
            PushUndoSnapshot();

            // Work on a copy and assign a NEW list: the ItemsControl only refreshes its item
            // containers when the collection instance (or a collection change) is observed.
            var blocks = new List<OverlayBlock>(_gamingOverlayBlueprint.Layout);
            mutate(blocks);
            _gamingOverlayBlueprint.Layout = blocks;

            OnPropertyChanged(nameof(GamingOverlayBlueprint));
            OnPropertyChanged(nameof(GamingOverlayBlueprintJson));
            OnPropertyChanged(nameof(GamingOverlayLayoutBlocks));
            OnPropertyChanged(nameof(GamingOverlayLayerItems));
            OnPropertyChanged(nameof(GamingOverlayGroups));
            OnPropertyChanged(nameof(GamingOverlayFirstGroupHidden));
            OnPropertyChanged(nameof(GamingOverlaySettingsButton));
            OnPropertyChanged(nameof(GamingOverlayCloseButton));
            OnPropertyChanged(nameof(GamingOverlayBackgroundLayer));
            OnPropertyChanged(nameof(GamingOverlayHasCombinedGauges));
            OnPropertyChanged(nameof(GamingOverlayHasMergeableGauges));
            OnPropertyChanged(nameof(SkinStudioGaugeLayoutActionText));
            RaiseCanvasGroupChanged();
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
                var groups = _gamingOverlayBlueprint.Groups;
                var defaultGroupId = groups.Count > 0 ? groups[0].Id : "main";

                foreach (var block in _gamingOverlayBlueprint.Layout)
                {
                    if (block == null || !block.IsVisible)
                    {
                        continue;
                    }

                    // The eye on a group folder hides every module inside it.
                    var blockGroupId = string.IsNullOrWhiteSpace(block.GroupId) ? defaultGroupId : block.GroupId;
                    var group = groups.FirstOrDefault(candidate =>
                        string.Equals(candidate.Id, blockGroupId, StringComparison.OrdinalIgnoreCase));

                    if (group is { IsHidden: true })
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