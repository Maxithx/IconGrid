using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using IconGrid.Models;
using IconGrid.ViewModels;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the window-arranging / layout-engine logic that previously lived in MainWindow.xaml.cs.
    /// Keeps the launcher shell focused on window lifetime and UI composition.
    /// </summary>
    public static class WindowLayoutEngine
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private static RECT GetWindowRect(IntPtr hWnd)
        {
            GetWindowRect(hWnd, out var rect);
            return rect;
        }

        private enum LayoutChoice
        {
            Auto,
            Favorite,
            TwoUp,
            ThreePane,
            ThreePaneMirror,
            Grid2x2
        }

        private record LayoutResolution(LayoutChoice Choice, string? SavedLayoutName);

        internal static void ArrangeWindowsFromPreset(
            string presetName,
            MainViewModel viewModel,
            IntPtr hwndSourceHandle,
            Action<string> log,
            Action refreshLayoutCardSelection)
        {
            try
            {
                var resolution = ResolvePreset(presetName, viewModel);
                var savedSlots = resolution.SavedLayoutName != null
                    ? viewModel.GetSavedLayoutSlots(resolution.SavedLayoutName).ToList()
                    : new List<CustomLayoutSlot>();

                var preset = resolution.Choice;
                var hasSaved = preset == LayoutChoice.Favorite && savedSlots.Any();

                var targetMonitor = viewModel.LayoutCurrentMonitorOnly && hwndSourceHandle != IntPtr.Zero
                    ? MonitorFromWindow(hwndSourceHandle, MONITOR_DEFAULTTONEAREST)
                    : IntPtr.Zero;

                var workArea = GetWorkArea(targetMonitor);
                var windows = CollectCandidateWindows(targetMonitor, viewModel.LayoutReserveIconGridSlot, hwndSourceHandle, viewModel, log);
                if (preset == LayoutChoice.Favorite)
                {
                    log($"Arrange(saved '{resolution.SavedLayoutName ?? viewModel.LayoutPreset}'): candidates={windows.Count}, workArea=({workArea.Left},{workArea.Top},{workArea.Right},{workArea.Bottom})");
                }

                if (windows.Count == 0)
                {
                    return;
                }

                var resolvedPreset = preset == LayoutChoice.Auto
                    ? SuggestPresetForCount(windows.Count)
                    : preset;

                if (preset == LayoutChoice.Favorite && !hasSaved)
                {
                    log("Saved preset selected without stored slots; falling back to auto grid.");
                    preset = SuggestPresetForCount(windows.Count);
                }

                var effectivePreset = preset == LayoutChoice.Auto ? resolvedPreset : preset;
                var slots = effectivePreset == LayoutChoice.Favorite
                    ? BuildSavedSlots(savedSlots, workArea, log)
                    : BuildSlots(effectivePreset, workArea, windows.Count, viewModel, log);

                var selectedSlot = Math.Max(0, Math.Min(viewModel.LayoutIconGridSlot, Math.Max(0, slots.Count - 1)));
                if (selectedSlot != viewModel.LayoutIconGridSlot)
                {
                    viewModel.LayoutIconGridSlot = selectedSlot;
                    refreshLayoutCardSelection();
                }

                var preferredSlot = viewModel.LayoutReserveIconGridSlot ? selectedSlot : -1;

                List<(IntPtr Hwnd, RECT Rect)> ordered;
                if (effectivePreset == LayoutChoice.Favorite)
                {
                    ordered = BuildFavoriteAssignments(windows, slots, hwndSourceHandle, preferredSlot, viewModel.LayoutReserveIconGridSlot, log);
                }
                else
                {
                    ordered = BuildOrderedAssignments(windows, slots, hwndSourceHandle == IntPtr.Zero ? (IntPtr?)null : hwndSourceHandle, preferredSlot, viewModel.LayoutReserveIconGridSlot, viewModel, log);
                }

                if (effectivePreset == LayoutChoice.Favorite)
                {
                    try
                    {
                        log($"Saved slots resolved: {slots.Count}; assignments={ordered.Count}; name={resolution.SavedLayoutName ?? presetName}");
                        log("Saved assignments detail: " + string.Join("; ", ordered.Select((a, idx) => $"slot{idx}:{DescribeHandle(a.Item1)}->{DescribeRect(a.Item2)}")));
                    }
                    catch
                    {
                        // best effort logging
                    }
                }

                var myHandle = hwndSourceHandle;
                foreach (var assignment in ordered)
                {
                    var hwnd = assignment.Hwnd;
                    if (hwnd == IntPtr.Zero)
                        continue;

                    var slot = assignment.Rect;

                    if (IsIconic(hwnd))
                    {
                        ShowWindow(hwnd, SW_RESTORE);
                    }

                    var width = Math.Max(100, slot.Right - slot.Left);
                    var height = Math.Max(100, slot.Bottom - slot.Top);

                    if (hwnd == myHandle)
                    {
                        continue;
                    }

                    if (myHandle == IntPtr.Zero)
                    {
                        // This is our own window. Use physical pixels (disable DPI scaling for layout test).
                        // By using physical coordinates here, vi afprøver om DPI transform er årsagen til ghost-box.
                        var currentWidth = width;
                        var currentHeight = height;
                        var iconLeft = slot.Left;
                        var iconTop = slot.Top;

                        SetWindowPos(hwnd, IntPtr.Zero, iconLeft, iconTop, currentWidth, currentHeight,
                            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    }
                    else
                    {
                        // External windows already use physical pixels.
                        SetWindowPos(hwnd, IntPtr.Zero, slot.Left, slot.Top, width, height,
                            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    }
                }

                if (viewModel.LayoutReserveIconGridSlot && myHandle != IntPtr.Zero && selectedSlot >= 0 && selectedSlot < slots.Count)
                {
                    MoveIconGridToReservedSlot(slots[selectedSlot], workArea, hwndSourceHandle);
                }
            }
            catch (Exception ex)
            {
                log("ArrangeWindows failed: " + ex);
            }
        }

        internal static bool IsExcludedWindow(IntPtr hwnd)
        {
            try
            {
                // Get window class name
                var className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                var classNameStr = className.ToString();

                // Exclude Explorer/File Manager windows
                if (classNameStr.Contains("ExploreWClass") || classNameStr.Contains("CabinetWClass"))
                    return true;

                // Exclude other system windows
                if (classNameStr.Contains("Shell_TrayWnd") || classNameStr.Contains("Progman"))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static List<(IntPtr Hwnd, RECT Rect)> BuildOrderedAssignments(
            List<(IntPtr Hwnd, RECT Rect)> windows,
            List<RECT> slots,
            IntPtr? iconGridHandle,
            int preferredIconSlot,
            bool reserveIconGridSlot,
            MainViewModel viewModel,
            Action<string> log)
        {
            var slotCount = slots.Count;
            var ordered = new (IntPtr Hwnd, RECT Rect)[slotCount];

            var remainingWindows = new List<(IntPtr Hwnd, RECT Rect)>(windows);
            var openSlots = Enumerable.Range(0, slotCount).ToList();

            // Reserve IconGrid slot even if IconGrid itself is not part of the arranged window set.
            if (reserveIconGridSlot && iconGridHandle.HasValue && iconGridHandle.Value != IntPtr.Zero && preferredIconSlot >= 0 && preferredIconSlot < slotCount)
            {
                var idx = remainingWindows.FindIndex(w => w.Hwnd == iconGridHandle.Value);
                if (idx >= 0)
                {
                    ordered[preferredIconSlot] = (remainingWindows[idx].Hwnd, slots[preferredIconSlot]);
                    remainingWindows.RemoveAt(idx);
                }

                openSlots.Remove(preferredIconSlot);
            }

            // If a link pair is defined for this preset, try to place the best matching windows into the link slots.
            if (viewModel.LayoutLinks.TryGetValue(viewModel.LayoutPreset, out var linkSlots) && linkSlots != null && linkSlots.Length == 2)
            {
                var linkA = Math.Max(0, Math.Min(linkSlots[0], slotCount - 1));
                var linkB = Math.Max(0, Math.Min(linkSlots[1], slotCount - 1));

                var targetLinkSlots = new[] { linkA, linkB }.Distinct().Where(openSlots.Contains).ToList();
                if (targetLinkSlots.Count == 2 && remainingWindows.Count > 0)
                {
                    var targetRects = targetLinkSlots.Select(i => slots[i]).ToList();
                    var matched = MatchWindowsToSlotsByCloseness(remainingWindows, targetRects);
                    for (int i = 0; i < matched.Count && i < targetLinkSlots.Count; i++)
                    {
                        var slotIndex = targetLinkSlots[i];
                        ordered[slotIndex] = (matched[i].Hwnd, slots[slotIndex]);
                        openSlots.Remove(slotIndex);
                        remainingWindows.Remove(matched[i]);
                    }
                }
            }

            // Match remaining windows to remaining slots
            var matchSlots = openSlots.Select(i => slots[i]).ToList();
            var assignments = MatchWindowsToSlotsWithSlots(remainingWindows, openSlots, slots);

            foreach (var (slotIndex, window) in assignments)
            {
                if (slotIndex >= 0 && slotIndex < ordered.Length)
                {
                    ordered[slotIndex] = (window.Hwnd, slots[slotIndex]);
                }
            }

            return ordered.ToList();
        }

        private static void MoveIconGridToReservedSlot(RECT slot, RECT workArea, IntPtr hwndSourceHandle)
        {
            var hwnd = hwndSourceHandle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var currentRect = GetWindowRect(hwnd);
            var currentWidth = Math.Max(100, currentRect.Right - currentRect.Left);
            var currentHeight = Math.Max(100, currentRect.Bottom - currentRect.Top);
            var slotWidth = Math.Max(1, slot.Right - slot.Left);
            var slotHeight = Math.Max(1, slot.Bottom - slot.Top);
            var workCenterX = workArea.Left + ((workArea.Right - workArea.Left) / 2);
            var workCenterY = workArea.Top + ((workArea.Bottom - workArea.Top) / 2);
            var slotCenterX = slot.Left + (slotWidth / 2);
            var slotCenterY = slot.Top + (slotHeight / 2);

            var left = currentWidth <= slotWidth
                ? slot.Left + ((slotWidth - currentWidth) / 2)
                : (slotCenterX >= workCenterX ? slot.Right - currentWidth : slot.Left);

            var top = currentHeight <= slotHeight
                ? slot.Top + ((slotHeight - currentHeight) / 2)
                : (slotCenterY >= workCenterY ? slot.Bottom - currentHeight : slot.Top);

            left = Math.Max(workArea.Left, Math.Min(left, workArea.Right - currentWidth));
            top = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - currentHeight));

            SetWindowPos(hwnd, IntPtr.Zero, left, top, currentWidth, currentHeight,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private static List<(int SlotIndex, (IntPtr Hwnd, RECT Rect) Window)> MatchWindowsToSlotsWithSlots(
            List<(IntPtr Hwnd, RECT Rect)> windows,
            List<int> slotIndices,
            List<RECT> allSlots)
        {
            var needed = Math.Min(windows.Count, slotIndices.Count);
            var best = new List<(int SlotIndex, (IntPtr Hwnd, RECT Rect) Window)>();
            double bestCost = double.MaxValue;
            var current = new (int SlotIndex, (IntPtr Hwnd, RECT Rect) Window)[needed];
            var usedSlots = new HashSet<int>();

            void Backtrack(int depth, double cost)
            {
                if (cost >= bestCost)
                    return;
                if (depth == needed)
                {
                    bestCost = cost;
                    best = current.ToList();
                    return;
                }

                for (int i = 0; i < slotIndices.Count; i++)
                {
                    var slotIndex = slotIndices[i];
                    if (usedSlots.Contains(slotIndex))
                        continue;

                    usedSlots.Add(slotIndex);
                    var slotRect = allSlots[slotIndex];
                    var added = DistanceCost(windows[depth].Rect, slotRect);
                    current[depth] = (slotIndex, windows[depth]);
                    Backtrack(depth + 1, cost + added);
                    usedSlots.Remove(slotIndex);
                }
            }

            Backtrack(0, 0);
            return best;
        }

        private static List<(IntPtr Hwnd, RECT Rect)> MatchWindowsToSlotsByCloseness(List<(IntPtr Hwnd, RECT Rect)> windows, List<RECT> slots)
        {
            var windowCount = windows.Count;
            var slotCount = Math.Min(slots.Count, windowCount);
            if (slotCount == 0)
                return new List<(IntPtr, RECT)>();

            if (slotCount <= 6)
            {
                var used = new bool[windowCount];
                var best = new (IntPtr Hwnd, RECT Rect)[slotCount];
                double bestCost = double.MaxValue;
                var current = new (IntPtr Hwnd, RECT Rect)[slotCount];

                void Backtrack(int depth, double cost)
                {
                    if (cost >= bestCost)
                        return;
                    if (depth == slotCount)
                    {
                        bestCost = cost;
                        Array.Copy(current, best, slotCount);
                        return;
                    }

                    for (int i = 0; i < windowCount; i++)
                    {
                        if (used[i]) continue;
                        used[i] = true;
                        current[depth] = windows[i];
                        var added = DistanceCost(windows[i].Rect, slots[depth]);
                        Backtrack(depth + 1, cost + added);
                        used[i] = false;
                    }
                }

                Backtrack(0, 0);
                var bestList = best.ToList();
                var bestHandles = new HashSet<IntPtr>(bestList.Select(b => b.Hwnd));
                bestList.AddRange(windows.Where(w => !bestHandles.Contains(w.Hwnd)));
                return bestList;
            }

            // Greedy fallback
            var remainingWindows = new List<(IntPtr Hwnd, RECT Rect)>(windows);
            var ordered = new List<(IntPtr Hwnd, RECT Rect)>();
            foreach (var slot in slots)
            {
                if (remainingWindows.Count == 0)
                    break;
                var nearest = remainingWindows.OrderBy(w => DistanceCost(w.Rect, slot)).First();
                ordered.Add(nearest);
                remainingWindows.Remove(nearest);
            }
            ordered.AddRange(remainingWindows);
            return ordered;
        }

        private static double DistanceCost(RECT rect, RECT slot)
        {
            var cx = rect.Left + (rect.Right - rect.Left) / 2.0;
            var cy = rect.Top + (rect.Bottom - rect.Top) / 2.0;
            var sx = slot.Left + (slot.Right - slot.Left) / 2.0;
            var sy = slot.Top + (slot.Bottom - slot.Top) / 2.0;
            var dx = cx - sx;
            var dy = cy - sy;
            return (dx * dx) + (dy * dy);
        }

        private static LayoutResolution ResolvePreset(string presetName, MainViewModel viewModel)
        {
            if (viewModel.TryGetSavedLayout(presetName, out _, out var canonical))
                return new LayoutResolution(LayoutChoice.Favorite, canonical);

            if (string.IsNullOrWhiteSpace(presetName))
                return new LayoutResolution(LayoutChoice.Auto, null);

            var key = presetName.Trim().ToLowerInvariant();
            return key switch
            {
                "twoup" => new LayoutResolution(LayoutChoice.TwoUp, null),
                "two-up" => new LayoutResolution(LayoutChoice.TwoUp, null),
                "favorite" => viewModel.TryGetSavedLayout("Favorit", out _, out var fallbackName)
                    ? new LayoutResolution(LayoutChoice.Favorite, fallbackName)
                    : new LayoutResolution(LayoutChoice.Auto, null),
                "threepane" => new LayoutResolution(LayoutChoice.ThreePane, null),
                "three-up" => new LayoutResolution(LayoutChoice.ThreePane, null),
                "threepanemirror" => new LayoutResolution(LayoutChoice.ThreePaneMirror, null),
                "3-up (mirror)" => new LayoutResolution(LayoutChoice.ThreePaneMirror, null),
                "grid2x2" => new LayoutResolution(LayoutChoice.Grid2x2, null),
                "grid" => new LayoutResolution(LayoutChoice.Grid2x2, null),
                _ => new LayoutResolution(LayoutChoice.Auto, null)
            };
        }

        private static LayoutChoice SuggestPresetForCount(int windowCount)
        {
            // Auto-suggest layout based on number of windows
            return windowCount switch
            {
                1 => LayoutChoice.Auto,  // Single window, leave as is
                2 => LayoutChoice.TwoUp,  // Two windows side-by-side
                3 => LayoutChoice.ThreePane,  // Three windows: one large on left, two stacked on right
                _ => LayoutChoice.Grid2x2  // Four or more windows: 2x2 grid
            };
        }

        internal static RECT GetWorkArea(IntPtr targetMonitor)
        {
            if (targetMonitor != IntPtr.Zero)
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(targetMonitor, ref info))
                {
                    return info.rcWork;
                }
            }

            var work = SystemParameters.WorkArea;
            return new RECT
            {
                Left = (int)work.Left,
                Top = (int)work.Top,
                Right = (int)work.Right,
                Bottom = (int)work.Bottom
            };
        }

        internal static List<(IntPtr Hwnd, RECT Rect)> CollectCandidateWindows(
            IntPtr targetMonitor,
            bool includeIconGridWindow,
            IntPtr iconGridHandle,
            MainViewModel viewModel,
            Action<string> log)
        {
            var result = new List<(IntPtr, RECT)>();
            var currentPid = Process.GetCurrentProcess().Id;

            EnumWindows((hwnd, lParam) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true;

                var styles = GetWindowLong(hwnd, GWL_EXSTYLE);
                if ((styles & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW)
                    return true;

                if (viewModel.LayoutSkipMinimized && IsIconic(hwnd))
                    return true;

                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == (uint)currentPid && !viewModel.IsFullWindowVisible)
                    return true;

                if (targetMonitor != IntPtr.Zero && viewModel.LayoutCurrentMonitorOnly)
                {
                    var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                    if (mon != targetMonitor)
                        return true;
                }

                if (!GetWindowRect(hwnd, out var rect))
                    return true;
                if (!includeIconGridWindow && iconGridHandle != IntPtr.Zero && hwnd == iconGridHandle)
                    return true;

                var w = rect.Right - rect.Left;
                var h = rect.Bottom - rect.Top;
                if (w < 120 || h < 120)
                    return true;

                var className = string.Empty;
                try
                {
                    var sbCls = new StringBuilder(256);
                    if (GetClassName(hwnd, sbCls, sbCls.Capacity) > 0)
                    {
                        className = sbCls.ToString().Trim();
                    }
                }
                catch
                {
                    // ignore class fetch errors
                }

                if (!string.IsNullOrEmpty(className) &&
                    className.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var titleLen = GetWindowTextLength(hwnd);
                if (titleLen <= 0 || titleLen > 512)
                    return true;

                var sb = new StringBuilder(Math.Min(512, titleLen + 10));
                GetWindowText(hwnd, sb, sb.Capacity);
                var title = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                result.Add((hwnd, rect));
                return true;
            }, IntPtr.Zero);

            try
            {
                log($"CollectCandidateWindows: count={result.Count}, skipMin={viewModel.LayoutSkipMinimized}, currentMonitorOnly={viewModel.LayoutCurrentMonitorOnly}, target={targetMonitor}");
                log("Candidates: " + string.Join("; ", result.Select(DescribeWindow)));
            }
            catch
            {
                // best effort logging
            }

            return result;
        }

        private static List<RECT> BuildSlots(LayoutChoice preset, RECT work, int windowCount, MainViewModel viewModel, Action<string> log)
        {
            var width = work.Right - work.Left;
            var height = work.Bottom - work.Top;
            var slots = new List<RECT>();

            RECT Make(int x, int y, int w, int h) => new RECT
            {
                Left = x,
                Top = y,
                Right = x + w,
                Bottom = y + h
            };

            switch (preset)
            {
                case LayoutChoice.Favorite:
                {
                    var favorite = BuildSavedSlots(viewModel.GetSavedLayoutSlots(viewModel.LayoutPreset), work, log);
                    if (favorite.Count > 0)
                    {
                        slots.AddRange(favorite);
                        break;
                    }
                    goto case LayoutChoice.Grid2x2;
                }
                case LayoutChoice.Grid2x2:
                case LayoutChoice.Auto:
                {
                    var halfW = width / 2;
                    var halfH = height / 2;
                    slots.Add(Make(work.Left, work.Top, halfW, halfH));
                    slots.Add(Make(work.Left + halfW, work.Top, width - halfW, halfH));
                    slots.Add(Make(work.Left, work.Top + halfH, halfW, height - halfH));
                    slots.Add(Make(work.Left + halfW, work.Top + halfH, width - halfW, height - halfH));
                    break;
                }
                case LayoutChoice.TwoUp:
                {
                    var half = width / 2;
                    slots.Add(Make(work.Left, work.Top, half, height));
                    slots.Add(Make(work.Left + half, work.Top, width - half, height));
                    break;
                }
                case LayoutChoice.ThreePane:
                {
                    var leftWidth = (int)(width * 0.5);
                    var rightWidth = width - leftWidth;
                    var halfHeight = height / 2;
                    slots.Add(Make(work.Left, work.Top, leftWidth, height));
                    slots.Add(Make(work.Left + leftWidth, work.Top, rightWidth, halfHeight));
                    slots.Add(Make(work.Left + leftWidth, work.Top + halfHeight, rightWidth, height - halfHeight));
                    break;
                }
                case LayoutChoice.ThreePaneMirror:
                {
                    var rightWidth = (int)(width * 0.5);
                    var leftWidth = width - rightWidth;
                    var halfHeight = height / 2;
                    slots.Add(Make(work.Left, work.Top, leftWidth, halfHeight));
                    slots.Add(Make(work.Left, work.Top + halfHeight, leftWidth, height - halfHeight));
                    slots.Add(Make(work.Left + leftWidth, work.Top, rightWidth, height));
                    break;
                }
            }

            return slots;
        }

        internal static CustomLayoutSlot NormalizeRectToWorkArea(RECT rect, RECT workArea)
        {
            var workWidth = Math.Max(1, workArea.Right - workArea.Left);
            var workHeight = Math.Max(1, workArea.Bottom - workArea.Top);

            double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

            var x = Clamp01((rect.Left - workArea.Left) / (double)workWidth);
            var y = Clamp01((rect.Top - workArea.Top) / (double)workHeight);
            var width = Clamp01((rect.Right - rect.Left) / (double)workWidth);
            var height = Clamp01((rect.Bottom - rect.Top) / (double)workHeight);

            width = Math.Min(width, 1 - x);
            height = Math.Min(height, 1 - y);

            return new CustomLayoutSlot
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            };
        }

        internal static bool SlotsClose(CustomLayoutSlot a, CustomLayoutSlot b, double tol)
        {
            return Math.Abs(a.X - b.X) <= tol
                   && Math.Abs(a.Y - b.Y) <= tol
                   && Math.Abs(a.Width - b.Width) <= tol
                   && Math.Abs(a.Height - b.Height) <= tol;
        }

        private static List<RECT> BuildSavedSlots(IReadOnlyList<CustomLayoutSlot> savedSlots, RECT work, Action<string> log)
        {
            var result = new List<RECT>();
            var workWidth = Math.Max(1, work.Right - work.Left);
            var workHeight = Math.Max(1, work.Bottom - work.Top);

            foreach (var slot in savedSlots)
            {
                var left = work.Left + (int)Math.Round(slot.X * workWidth);
                var top = work.Top + (int)Math.Round(slot.Y * workHeight);
                var width = (int)Math.Round(slot.Width * workWidth);
                var height = (int)Math.Round(slot.Height * workHeight);

                width = Math.Max(120, width);
                height = Math.Max(120, height);

                left = Math.Max(work.Left, Math.Min(left, work.Right - width));
                top = Math.Max(work.Top, Math.Min(top, work.Bottom - height));

                var right = Math.Min(work.Right, left + width);
                var bottom = Math.Min(work.Bottom, top + height);

                result.Add(new RECT
                {
                    Left = left,
                    Top = top,
                    Right = right,
                    Bottom = bottom
                });
            }

            try
            {
                log("Saved normalized slots: " + string.Join("; ", savedSlots.Select(s => $"({s.X:F3},{s.Y:F3},{s.Width:F3},{s.Height:F3})")));
                log("Saved realized slots: " + string.Join("; ", result.Select(DescribeRect)));
            }
            catch
            {
                // best effort logging
            }

            return result;
        }

        private static List<(IntPtr Hwnd, RECT Rect)> BuildFavoriteAssignments(
            List<(IntPtr Hwnd, RECT Rect)> windows,
            List<RECT> slots,
            IntPtr iconGridHandle,
            int preferredIconSlot,
            bool reserveIconGridSlot,
            Action<string> log)
        {
            var slotCount = slots.Count;
            var assignments = Enumerable.Repeat((IntPtr.Zero, new RECT()), slotCount).ToArray();

            var remainingWindows = new List<(IntPtr Hwnd, RECT Rect)>(windows);
            var availableSlots = Enumerable.Range(0, slotCount).ToList();

            // Reserve IconGrid's saved slot even if IconGrid itself is not part of the arranged window set.
            if (reserveIconGridSlot && iconGridHandle != IntPtr.Zero && preferredIconSlot >= 0 && preferredIconSlot < slotCount)
            {
                var idx = remainingWindows.FindIndex(w => w.Hwnd == iconGridHandle);
                if (idx >= 0)
                {
                    assignments[preferredIconSlot] = (iconGridHandle, slots[preferredIconSlot]);
                    remainingWindows.RemoveAt(idx);
                }

                availableSlots.Remove(preferredIconSlot);
            }

            if (remainingWindows.Count == 0 || availableSlots.Count == 0)
            {
                return assignments.ToList();
            }

            // Match remaining windows to remaining slots by closeness.
            var matches = MatchWindowsToSlotsWithSlots(remainingWindows, availableSlots, slots);
            foreach (var (slotIndex, window) in matches)
            {
                if (slotIndex >= 0 && slotIndex < assignments.Length)
                {
                    assignments[slotIndex] = (window.Hwnd, slots[slotIndex]);
                }
            }

            var finalAssignments = assignments.ToList();

            try
            {
                log("Favorite assignment mapping: " + string.Join("; ", finalAssignments.Select((a, idx) => $"slot{idx}:{DescribeHandle(a.Item1)}->{DescribeRect(a.Item2)}")));
            }
            catch
            {
                // ignore log issues
            }

            return finalAssignments;
        }

        private static string DescribeHandle(IntPtr hwnd) => hwnd == IntPtr.Zero ? "null" : $"0x{hwnd.ToInt64():X}";

        private static string DescribeWindow((IntPtr hwnd, RECT rect) w)
        {
            string title = string.Empty;
            try
            {
                var len = GetWindowTextLength(w.hwnd);
                if (len > 0 && len < 512)
                {
                    var sb = new StringBuilder(len + 5);
                    GetWindowText(w.hwnd, sb, sb.Capacity);
                    title = sb.ToString().Trim();
                }
            }
            catch
            {
                // ignore title fetch errors
            }

            string cls = string.Empty;
            try
            {
                var sbCls = new StringBuilder(256);
                if (GetClassName(w.hwnd, sbCls, sbCls.Capacity) > 0)
                {
                    cls = sbCls.ToString().Trim();
                }
            }
            catch
            {
                // ignore class fetch errors
            }

            GetWindowThreadProcessId(w.hwnd, out var pid);
            var titlePart = string.IsNullOrWhiteSpace(title) ? "" : $" \"{title}\"";
            var classPart = string.IsNullOrWhiteSpace(cls) ? "" : $" [{cls}]";
            return $"{DescribeHandle(w.hwnd)}:{DescribeRect(w.rect)} pid={pid}{titlePart}{classPart}";
        }

        private static string DescribeRect(RECT rect) => $"({rect.Left},{rect.Top},{rect.Right - rect.Left}x{rect.Bottom - rect.Top})";
    }
}