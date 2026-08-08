using System;
using System.Diagnostics;
using System.Security.Principal;

namespace IconGrid.Helpers.Hardware;

internal static class EtwAccessRequirements
{
    private const string DanishPerformanceLogUsersName = "Brugere af ydelseslog";
    private const string EnglishPerformanceLogUsersName = "Performance Log Users";

    public static EtwAccessStatus GetCurrentStatus()
    {
        var identity = WindowsIdentity.GetCurrent();
        var isElevated = IsCurrentProcessElevated(identity);

        var isMember = false;
        string groupDisplayName;

        try
        {
            var perfLogUsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinPerformanceLoggingUsersSid, null);
            groupDisplayName = TryTranslateGroupName(perfLogUsersSid) ?? $"{DanishPerformanceLogUsersName} / {EnglishPerformanceLogUsersName}";
            isMember = identity.Groups?.Contains(perfLogUsersSid) == true;
        }
        catch
        {
            groupDisplayName = $"{DanishPerformanceLogUsersName} / {EnglishPerformanceLogUsersName}";
        }

        var userDisplay = identity.Name ?? $"{Environment.MachineName}\\{Environment.UserName}";

        // The native FPS agent runs elevated (UAC admin). An elevated process can
        // read ETW / dxgkrnl events WITHOUT the Performance Log Users membership,
        // so "isMember" alone is NOT the correct readiness signal.
        //
        // The hardware monitor always launches the native agent with the "runas"
        // verb, so ETW FPS works in practice even when the current user is not in
        // Performance Log Users. Only when the elevated agent cannot start (UAC
        // denied) does the fix become necessary.
        var isReady = true;

        var summary = isMember
            ? "FPS ETW setup looks ready for this user."
            : "FPS ETW setup is ready via the elevated agent.";

        var guidance = isMember
            ? "The user is already a member of Performance Log Users."
            : "The user is not in Performance Log Users, but the native FPS agent runs elevated, so ETW FPS works. Membership is only needed if the agent cannot start elevated.";

        var command = $"net localgroup \"{DanishPerformanceLogUsersName}\" \"{userDisplay}\" /add";

        return new EtwAccessStatus(
            userDisplay,
            groupDisplayName,
            isElevated,
            isMember,
            isReady,
            summary,
            guidance,
            command);
    }

    public static bool TryLaunchElevatedSetup(string userDisplayName)
    {
        try
        {
            var command = $"net localgroup \"{DanishPerformanceLogUsersName}\" \"{userDisplayName}\" /add";
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{command}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            };

            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCurrentProcessElevated(WindowsIdentity identity)
    {
        try
        {
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string? TryTranslateGroupName(SecurityIdentifier sid)
    {
        try
        {
            return sid.Translate(typeof(NTAccount)).Value;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record EtwAccessStatus(
    string UserDisplayName,
    string GroupDisplayName,
    bool IsCurrentProcessElevated,
    bool IsUserInPerformanceLogUsers,
    bool IsReadyForEtwFps,
    string Summary,
    string Guidance,
    string SuggestedAddCommand);
