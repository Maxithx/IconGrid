namespace IconGrid.Models;

/// <summary>
/// JSON-serializable snapshot of the monitor layout properties (18 monitor row values
/// plus the gaming overlay background height).
/// Used by the "Save current as default" / "Reset defaults" buttons on MonitorRowLayoutPage.
/// </summary>
public class MonitorLayoutDefaultsSnapshot
{
    // ── Element gaps ──
    public double MonitorPingToNetGap { get; set; } = 4;
    public double MonitorNetToDownloadGap { get; set; } = 4;
    public double MonitorDownloadToUploadGap { get; set; } = 4;
    public double MonitorUploadToCpuGap { get; set; } = 4;
    public double MonitorCpuToGpuGap { get; set; } = 4;

    // ── Dividers ──
    public bool MonitorDivider0Visible { get; set; } = true;
    public bool MonitorDivider1Visible { get; set; } = true;
    public bool MonitorDivider2Visible { get; set; } = true;
    public bool MonitorDivider3Visible { get; set; } = true;
    public double MonitorDividerGap { get; set; } = 16;

    // ── Bar gaps ──
    public double MonitorCpuBarGap { get; set; } = 8;
    public double MonitorGpuBarGap { get; set; } = 8;

    // ── Label/value micro-gaps ──
    public double MonitorDownloadLabelToValueGap { get; set; } = 4;
    public double MonitorUploadLabelToValueGap { get; set; } = 4;
    public double MonitorDownloadValueToUnitGap { get; set; } = 4;
    public double MonitorUploadValueToUnitGap { get; set; } = 4;

    // ── Value widths ──
    public double MonitorDownloadValueWidth { get; set; } = 20;
    public double MonitorUploadValueWidth { get; set; } = 20;

    // ── Gaming overlay bar background height (px) ──
    // Nullable so snapshots saved before this property existed leave the user's
    // current overlay height untouched when "Reset to my default" is used.
    public double? GamingOverlayBackgroundHeight { get; set; }
}