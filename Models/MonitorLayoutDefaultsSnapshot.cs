namespace IconGrid.Models;

/// <summary>
/// JSON-serializable snapshot of all 18 monitor layout properties.
/// Used by the "Save current as default" / "Reset defaults" buttons on MonitorRowLayoutPage.
/// </summary>
public class MonitorLayoutDefaultsSnapshot
{
    // ── Element gaps ──
    public double MonitorPingToNetGap { get; set; } = 2;
    public double MonitorNetToDownloadGap { get; set; } = 2;
    public double MonitorDownloadToUploadGap { get; set; } = 2;
    public double MonitorUploadToCpuGap { get; set; } = 2;
    public double MonitorCpuToGpuGap { get; set; } = 2;

    // ── Dividers ──
    public bool MonitorDivider0Visible { get; set; } = true;
    public bool MonitorDivider1Visible { get; set; } = true;
    public bool MonitorDivider2Visible { get; set; } = true;
    public bool MonitorDivider3Visible { get; set; } = true;
    public double MonitorDividerGap { get; set; } = 0;

    // ── Bar gaps ──
    public double MonitorCpuBarGap { get; set; } = 0;
    public double MonitorGpuBarGap { get; set; } = 0;

    // ── Label/value micro-gaps ──
    public double MonitorDownloadLabelToValueGap { get; set; } = 0;
    public double MonitorUploadLabelToValueGap { get; set; } = 0;
    public double MonitorDownloadValueToUnitGap { get; set; } = 0;
    public double MonitorUploadValueToUnitGap { get; set; } = 0;

    // ── Value widths ──
    public double MonitorDownloadValueWidth { get; set; } = 0;
    public double MonitorUploadValueWidth { get; set; } = 0;
}