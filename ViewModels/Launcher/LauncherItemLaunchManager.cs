using System;
using System.Diagnostics;
using System.IO;
using IconGrid.Models;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherItemLaunchManager
    {
        public bool LaunchItem(LauncherItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return false;

            try
            {
                var psi = new ProcessStartInfo(item.Path)
                {
                    UseShellExecute = true
                };

                var workingDirectory = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                if (!string.IsNullOrWhiteSpace(item.Arguments))
                {
                    psi.Arguments = item.Arguments;
                }

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch {item.Path}: {ex}");
                return false;
            }
        }
    }
}
