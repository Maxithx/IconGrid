using System;
using System.Diagnostics;
using System.IO;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Central policy that separates real game processes from desktop/shell
/// programs. The hardware monitor (FPS target selection) and the launcher's
/// resolution lock both use this so there is a single authority for
/// "is this process a game?".
///
/// Two independent layers:
///  1. Identity — process names and Windows system paths that can never be a
///     game (TextInputHost, LockApp, taskmgr, shell apps, launchers, editors,
///     browsers, media/capture utilities...).
///  2. Evidence — the ETW frame signal the native FPS agent observed for a PID.
///     Real application presents (DXGI/D3D9) are strong evidence; the coarse
///     DxgKrnl kernel fallback is weaker and is never sufficient on its own for
///     a Windows system process.
/// </summary>
public static class GameProcessClassifier
{
    /// <summary>
    /// Processes that must never be treated as a game, even when they own a
    /// large visible window or emit a stray present event. Shared by the FPS
    /// target selection and the game-resolution lock so both pipelines agree.
    /// </summary>
    public static readonly string[] NonGameProcessNames =
    {
        // Windows shell / system UI (source of the worst false positives).
        "explorer", "ApplicationFrameHost", "SearchApp", "SearchHost",
        "StartMenuExperienceHost", "ShellExperienceHost", "ShellHost",
        "TextInputHost", "LockApp", "Widgets", "RuntimeBroker", "dwm",
        "SystemSettings", "mscopilot", "WmiPrvSE", "Taskmgr", "IconGrid",
        "SecurityHealthSystray", "SecurityHealthService", "conhost",
        "XboxGameBar", "XboxGameBarWidgets", "GameBar", "GameBarPresenceWriter",
        // Stores / game launchers (never the game itself).
        "Battle.net", "steam", "steamwebhelper", "upc", "EADesktop",
        "EpicGamesLauncher", "launcher", "UbisoftConnect",
        // Browsers / editors / terminals.
        "Code", "devenv", "brave", "chrome", "msedge", "msedgewebview2",
        "firefox", "WindowsTerminal", "OpenConsole", "cmd", "powershell", "pwsh",
        // Media / capture / productivity / chat.
        "notepad", "mspaint", "SnippingTool", "ScreenSketch", "Photos",
        "Microsoft.Photos", "Microsoft.Media.Player", "ZuneMusic", "Music.UI",
        "Photoshop", "Illustrator", "olk", "OUTLOOK", "WINWORD", "EXCEL", "POWERPNT",
        "ms-teams", "Teams", "Spotify", "Discord", "OneDrive",
        // Overlays / utilities that are not games.
        "nvcontainer", "NVIDIA Share", "RadeonSoftware", "AMDRSServ",
        "qbittorrent", "KeePassXC"
    };

    private static readonly string[] NonGamePathPrefixes = BuildNonGamePathPrefixes();

    private static string[] BuildNonGamePathPrefixes()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
        {
            windows = @"C:\Windows";
        }

        var separator = Path.DirectorySeparatorChar;
        return new[]
        {
            // No real game ships from inside the Windows system directories, and
            // these roots hold the shell/system apps that generated the false
            // positives (TextInputHost, LockApp, SearchHost, taskmgr, ...).
            Path.Combine(windows, "SystemApps") + separator,
            Path.Combine(windows, "system32") + separator,
            Path.Combine(windows, "SysWOW64") + separator
        };
    }

    /// <summary>
    /// True when the process name is a known non-game program.
    /// </summary>
    public static bool IsNonGameProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (var ignored in NonGameProcessNames)
        {
            if (string.Equals(processName, ignored, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the executable lives under a Windows system directory.
    /// </summary>
    public static bool IsNonGameProcessPath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        foreach (var prefix in NonGamePathPrefixes)
        {
            if (executablePath!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when either the name or the resolved path marks the process as a
    /// non-game program.
    /// </summary>
    public static bool IsNonGameProcess(string? processName, string? executablePath)
        => IsNonGameProcessName(processName) || IsNonGameProcessPath(executablePath);

    /// <summary>
    /// Best-effort executable path lookup. Returns null when MainModule is
    /// blocked (elevated anti-cheat) or the process is gone — callers must
    /// treat null as "unknown", never as a positive signal.
    /// </summary>
    public static string? TryGetProcessPath(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// True when the native agent observed strong, application-level present
    /// evidence for the target: at least two DXGI or D3D9 presents. This is the
    /// signal that reliably separates a rendering game from a shell process.
    /// </summary>
    public static bool HasPrimaryGraphicsEvidence(NativeFpsAgentState? nativeState, int minimumEvents = 2)
    {
        if (nativeState == null)
        {
            return false;
        }

        return nativeState.MatchedDxgiEventCount + nativeState.MatchedD3D9EventCount >= minimumEvents;
    }

    /// <summary>
    /// True when the native agent observed any usable frame evidence. DXGI/D3D9
    /// application presents are strong; the DxgKrnl kernel fallback is weaker
    /// and is only accepted at the minimum threshold the native agent itself
    /// requires (two samples at >=20fps) before it reports a kernel frame rate.
    /// </summary>
    public static bool IsGameEvidence(NativeFpsAgentState? nativeState, int minimumEvents = 2)
    {
        if (nativeState == null)
        {
            return false;
        }

        if (HasPrimaryGraphicsEvidence(nativeState, minimumEvents))
        {
            return true;
        }

        return nativeState.MatchedDxgKrnlEventCount >= minimumEvents;
    }
}
