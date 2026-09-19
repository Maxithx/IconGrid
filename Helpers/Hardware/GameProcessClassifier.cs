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
    /// The only identities that are "never a game" by NAME: our own processes.
    ///
    /// Everything else is decided by evidence (see <see cref="IsGameEvidence"/> /
    /// <see cref="HasPrimaryGraphicsEvidence"/>), because an ever-growing list of
    /// "programs that are not games" is a losing game: a new shell element / utility
    /// / overlay always appears that is not on the list, and it then gets detected
    /// as a game. That is exactly how TextInputHost, PowerToys.Peek.UI and the
    /// Windows Command Palette were mis-detected as games, each time blocking the
    /// real game from being acquired.
    ///
    /// The FPS pipeline must NOT consult an app blocklist for classification.
    /// </summary>
    public static readonly string[] SelfProcessNames =
    {
        "IconGrid", "IconGridFpsAgent"
    };

    /// <summary>
    /// Launchers / stores / bootstrappers that belong to a launch session but are
    /// never the game itself. Used ONLY to disambiguate a KNOWN launch (the
    /// process-tree matching behind the resolution lock and the window handoff),
    /// never to classify an arbitrary foreground process as a game.
    /// </summary>
    public static readonly string[] LaunchInfrastructureProcessNames =
    {
        "Battle.net", "steam", "steamwebhelper", "upc", "EADesktop",
        "EpicGamesLauncher", "launcher", "UbisoftConnect",
        "conhost", "cmd", "powershell", "pwsh", "WindowsTerminal", "OpenConsole"
    };

    /// <summary>
    /// Identity set for the launch/resolution path (self + launch infrastructure +
    /// Windows shell hosts). This exists so a shell host cannot steal the
    /// "game confirmed" handoff while a known launch is still starting. It is NOT
    /// used by the FPS classification path anymore.
    /// </summary>
    public static readonly string[] NonGameProcessNames = BuildNonGameProcessNames();

    private static string[] BuildNonGameProcessNames()
    {
        string[] shellHosts =
        {
            "explorer", "ApplicationFrameHost", "SearchApp", "SearchHost",
            "StartMenuExperienceHost", "ShellExperienceHost", "ShellHost",
            "TextInputHost", "LockApp", "Widgets", "RuntimeBroker", "dwm",
            "SystemSettings", "Taskmgr", "SecurityHealthSystray", "SecurityHealthService",
            "XboxGameBar", "XboxGameBarWidgets", "GameBar", "GameBarPresenceWriter"
        };

        var combined = new string[SelfProcessNames.Length + LaunchInfrastructureProcessNames.Length + shellHosts.Length];
        SelfProcessNames.CopyTo(combined, 0);
        LaunchInfrastructureProcessNames.CopyTo(combined, SelfProcessNames.Length);
        shellHosts.CopyTo(combined, SelfProcessNames.Length + LaunchInfrastructureProcessNames.Length);
        return combined;
    }

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
    /// True when the process is one of our own processes. The FPS pipeline uses
    /// this (plus the structural path rules and ETW evidence) instead of an app
    /// blocklist.
    /// </summary>
    public static bool IsSelfProcessName(string? processName)
        => MatchesAnyName(processName, SelfProcessNames);

    /// <summary>
    /// True when the process name is in the launch/resolution identity set
    /// (self + launch infrastructure + Windows shell hosts). Used to disambiguate
    /// a known launch session, not to classify arbitrary foreground processes.
    /// </summary>
    public static bool IsNonGameProcessName(string? processName)
        => MatchesAnyName(processName, NonGameProcessNames);

    private static bool MatchesAnyName(string? processName, string[] names)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (var ignored in names)
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

    private static readonly string[] WindowsStorePathPrefixes = BuildWindowsStorePathPrefixes();

    private static string[] BuildWindowsStorePathPrefixes()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(programFiles))
        {
            programFiles = @"C:\Program Files";
        }

        var separator = Path.DirectorySeparatorChar;
        return new[] { Path.Combine(programFiles, "WindowsApps") + separator };
    }

    /// <summary>
    /// True when the executable is packaged as a Microsoft Store / MSIX app
    /// (under Program Files\WindowsApps). That folder hosts real games (Game
    /// Pass) but also shell companions (Command Palette, PowerToys) that own
    /// large windows and emit a stray kernel present. Such processes therefore
    /// need stronger evidence before they are trusted as a game.
    /// </summary>
    public static bool IsWindowsStoreAppPath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        foreach (var prefix in WindowsStorePathPrefixes)
        {
            if (executablePath!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
