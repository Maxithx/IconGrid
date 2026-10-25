using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace IconGrid.Helpers.Hardware.Storage;

/// <summary>
/// A single SMART attribute reported by a physical disk.
/// <see cref="Status"/> holds a language-neutral code ("OK"/"Warning"); the UI localizes it.
/// </summary>
public sealed class SmartAttributeStatus
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Attribute value as reported by LibreHardwareMonitor (normalized or raw, drive dependent).</summary>
    public double Value { get; set; }

    /// <summary>SMART failure threshold. 0 means the attribute has no threshold.</summary>
    public int Threshold { get; set; }

    public string Status { get; set; } = SmartAttributeStatusCodes.Ok;

    [JsonIgnore]
    public string IdText => Id.ToString(CultureInfo.CurrentCulture);

    [JsonIgnore]
    public string ValueText => Value.ToString("0.#", CultureInfo.CurrentCulture);

    [JsonIgnore]
    public string ThresholdText => Threshold > 0 ? Threshold.ToString(CultureInfo.CurrentCulture) : "—";
}

/// <summary>Language-neutral SMART attribute status codes.</summary>
public static class SmartAttributeStatusCodes
{
    public const string Ok = "OK";
    public const string Warning = "Warning";
}
