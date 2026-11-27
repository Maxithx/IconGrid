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
            groupDisplayName = TryTranslateGroupName(perfLogUsersSid) ?? DanishPerformanceLogUsersName;
            isMember = identity.Groups?.Contains(perfLogUsersSid) == true;
        }
        catch
        {
            groupDisplayName = DanishPerformanceLogUsersName;
        }

        var userDisplay = identity.Name ?? $"{Environment.MachineName}\\{Environment.UserName}";

        // Membership is the only requirement this page can check directly. The elevated
        // native FPS agent may still read ETW without it, but the admin fix must stay
        // available when the user is missing from the group so the UI does not hide the
        // only repair path.
        var isReady = isMember;

        var summary = isMember
            ? "FPS ETW setup looks ready for this user."
            : "Performance Log Users membership is missing. FPS may still work through the elevated agent, but run the fix if FPS stays --.";

        var guidance = isMember
            ? "The user is already a member of Performance Log Users."
            : "Run the admin fix if FPS does not work. The elevated FPS agent may avoid the need for membership, but the group is the reliable fallback.";

        var command = $"net localgroup \"{groupDisplayName}\" \"{userDisplay}\" /add";

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

    public static bool TryLaunchElevatedSetup(string userDisplayName, string? groupDisplayName = null)
    {
        try
        {
            // Resolve the built-in group by its well-known SID instead of relying on a
            // localized display name. This avoids failures when the translated name
            // includes a scope such as "BUILTIN\Brugere af ydelseslog".
            var escapedUser = userDisplayName.Replace("'", "''");
            var script =
                "$g = Get-LocalGroup -SID 'S-1-5-32-559' -ErrorAction Stop; " +
                $"Add-LocalGroupMember -Group $g -Member '{escapedUser}' -ErrorAction Stop; " +
                $"Write-Host ('Added {escapedUser} to ' + $g.Name + '.'); " +
                "Start-Sleep -Seconds 2";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{script}\"",
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
