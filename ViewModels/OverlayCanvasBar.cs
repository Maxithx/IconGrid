using System;
using System.Collections.Generic;
using System.Windows;
using IconGrid.Models;

namespace IconGrid.ViewModels
{
    /// <summary>
    /// One overlay bar on the Skin Studio canvas: a layer group with its own modules, its own
    /// position and its own bar size. Every group is an independent bar, so the standard overlay
    /// and the bar being built stay visible at the same time.
    /// </summary>
    public sealed class OverlayCanvasBar
    {
        /// <summary>Canvas origin (DIPs): the base position every group offset is added to.</summary>
        public const double OriginLeft = 24;
        public const double OriginTop = 24;

        /// <summary>How far a bar may be dragged past the origin (keeps it on the canvas).</summary>
        public const double MinOffset = -24;

        public OverlayCanvasBar(OverlayLayerGroup group, IReadOnlyList<OverlayBlock> blocks, bool isSelected)
        {
            Group = group;
            Blocks = blocks;
            IsSelected = isSelected;
        }

        public OverlayLayerGroup Group { get; }

        public IReadOnlyList<OverlayBlock> Blocks { get; }

        public bool IsSelected { get; }

        public string GroupId => Group.Id;

        public string Title => Group.Name;

        public double CanvasLeft => OriginLeft + Group.X;

        public double CanvasTop => OriginTop + Group.Y;

        /// <summary>Fixed bar width when the group defines one; NaN lets the bar size to content.</summary>
        public double BarWidth => Group.Width > 0 ? Group.Width : double.NaN;

        /// <summary>Fixed bar height when the group defines one; NaN keeps the skin's bar height.</summary>
        public double BarHeight => Group.Height > 0 ? Group.Height : double.NaN;

        /// <summary>Bright guide frame for the group being designed, subtle frame for the others.</summary>
        public Visibility GuideVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PlainFrameVisibility => IsSelected ? Visibility.Collapsed : Visibility.Visible;
    }
}
