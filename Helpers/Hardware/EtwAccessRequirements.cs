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
        var isReady = isMember;

        var summary = isReady
            ? "FPS ETW setup looks ready for this user."
            : "This user is missing the ETW FPS access requirement.";

        var guidance = isReady
            ? "The user is already a member of Performance Log Users. If FPS still fails, the blocker is somewhere else."
            : $"Add the user to '{groupDisplayName}' from an elevated admin context, then sign out/in or restart Windows.";

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
