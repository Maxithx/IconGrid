using System.IO;
using IconGrid.Helpers;
using IconGrid.Models;

namespace IconGrid.ViewModels
{
    public class LauncherItemIconManager
    {
        public bool UpdateItemIcon(LauncherItem? item, string iconPath, int iconIndex)
        {
            if (item == null || string.IsNullOrWhiteSpace(iconPath))
                return false;

            if (Directory.Exists(iconPath))
            {
                var folderIcon = IconHelper.TryGetFolderIconFromDesktopIni(iconPath);
                if (folderIcon.HasValue && !string.IsNullOrWhiteSpace(folderIcon.Value.Path))
                {
                    iconPath = folderIcon.Value.Path!;
                    iconIndex = folderIcon.Value.Index;
                }
            }

            item.IconPath = iconPath;
            item.IconIndex = iconIndex;
            item.RefreshIcon();

            if (item.IconImage != null || !File.Exists(iconPath))
                return true;

            var img = IconHelper.GetIconFromLibrary(iconPath, iconIndex)
                     ?? IconHelper.GetHighResIcon(iconPath);
            if (img == null)
                return true;

            var trimmed = IconHelper.TrimTransparentBorder(img) ?? img;
            item.Icon = trimmed;
            item.IconBase64 = IconHelper.BitmapSourceToBase64(trimmed);
            return true;
        }

        public void EnsureItemIconLoaded(LauncherItem? item)
        {
            if (item == null || item.IconImage != null)
                return;

            var targetPath = !string.IsNullOrWhiteSpace(item.IconPath) ? item.IconPath : item.Path;
            if (string.IsNullOrWhiteSpace(targetPath))
                return;

            UpdateItemIcon(item, targetPath, item.IconIndex);
        }
    }
}
