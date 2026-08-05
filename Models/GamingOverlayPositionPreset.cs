namespace IconGrid.Models
{
    /// <summary>
    /// Standard placement presets for the gaming overlay, mirroring the corner presets
    /// used by popular overlay tools (e.g. FPS Overlay): pick one corner/edge and the
    /// overlay snaps there whenever it opens or the display resolution changes.
    /// "Custom" means the user drags the overlay anywhere and that position is kept.
    /// </summary>
    public enum GamingOverlayPositionPreset
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        Custom
    }
}