namespace IconGrid.Models
{
    /// <summary>
    /// Determines how the launcher hides itself when no game is running.
    /// </summary>
    public enum LauncherHideMode
    {
        /// <summary>Launcher stays visible at all times (default).</summary>
        AlwaysVisible = 0,

        /// <summary>User clicks a button to hide the launcher; clicks the peek strip to show it again.</summary>
        Manual = 1,

        /// <summary>Launcher auto-hides after a delay when the mouse leaves; auto-peeks when the mouse approaches the top edge.</summary>
        Auto = 2
    }
}