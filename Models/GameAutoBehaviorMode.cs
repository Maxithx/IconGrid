namespace IconGrid.Models;

/// <summary>
/// Launcher behavior while a game is running (chosen on the Gaming Overlay settings page).
/// </summary>
public enum GameAutoBehaviorMode
{
    /// <summary>No automatic behavior; the launcher stays as it is.</summary>
    None,

    /// <summary>Slide the launcher down out of view (auto-hide); the user can bring it
    /// back by moving the mouse to the screen edge. Works even with "Always on top".</summary>
    AutoHide,

    /// <summary>Minimize the launcher to the taskbar while a game is running.</summary>
    MinimizeToTaskbar
}