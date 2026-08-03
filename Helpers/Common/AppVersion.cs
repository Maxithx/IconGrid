using System.Reflection;

namespace IconGrid.Helpers.Common
{
    /// <summary>
    /// Exposes the assembly's informational version for UI display.
    /// Kept as a static, side-effect-free helper so views can bind it with x:Static.
    /// </summary>
    public static class AppVersion
    {
        public static string InformationalVersion { get; } =
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "unknown";
    }
}