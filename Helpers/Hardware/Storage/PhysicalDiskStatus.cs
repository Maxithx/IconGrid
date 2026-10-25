using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace IconGrid.Helpers.Hardware.Storage;

/// <summary>
/// One physical disk as published by the elevated monitor agent in
/// <c>monitor-state.json</c> and rendered on the Hardware page.
/// Instances are long-lived on the launcher side: <see cref="CopyFrom"/> updates
/// them in place so open SMART expanders keep their state.
/// </summary>
public sealed class PhysicalDiskStatus : INotifyPropertyChanged
{
    private int? _healthPercent;
    private int _index;
    private string _model = "--";
    private string _serial = string.Empty;
    private string _firmware = string.Empty;
    private string _driveLetters = string.Empty;
    private long _sizeBytes;
    private string _busType = DiskBusTypes.Unknown;
    private string _mediaType = DiskMediaTypeCodes.Unknown;
    private bool _isHdd;
    private bool _isSata;
    private int? _rotationRpm;
    private string _health = DiskHealthCodes.Unknown;
    private string _smartStatus = DiskSmartStatusCodes.Unavailable;
    private double? _temperatureC;
    private string _powerState = DiskPowerStateCodes.Unknown;
    private double? _activityPercent;
    private double? _readBytesPerSec;
    private double? _writeBytesPerSec;
    private DateTime? _smartCapturedAtUtc;
    private List<SmartAttributeStatus> _smartAttributes = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index { get => _index; set => SetField(ref _index, value); }

    public string Model { get => _model; set => SetField(ref _model, value); }

    public string Serial { get => _serial; set => SetField(ref _serial, value); }

    public string Firmware { get => _firmware; set => SetField(ref _firmware, value); }

    /// <summary>Drive letters assigned to this disk (e.g. "C:, D:"), empty when unpartitioned.</summary>
    public string DriveLetters { get => _driveLetters; set => SetField(ref _driveLetters, value); }

    /// <summary>Estimated health percentage (0-100) derived from SMART wear/critical attributes.</summary>
    public int? HealthPercent
    {
        get => _healthPercent;
        set
        {
            if (!SetField(ref _healthPercent, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HealthPercentText));
            OnPropertyChanged(nameof(HasHealthPercent));
        }
    }

    public long SizeBytes { get => _sizeBytes; set => SetField(ref _sizeBytes, value); }

    /// <summary>Bus type code: SATA, NVMe, USB, RAID, SAS, ATA, Unknown.</summary>
    public string BusType { get => _busType; set => SetField(ref _busType, value); }

    /// <summary>Media type code: SSD, HDD, Unknown.</summary>
    public string MediaType { get => _mediaType; set => SetField(ref _mediaType, value); }

    public bool IsHdd { get => _isHdd; set => SetField(ref _isHdd, value); }

    public bool IsSata { get => _isSata; set => SetField(ref _isSata, value); }

    /// <summary>Nominal spindle speed when the platform reports it.</summary>
    public int? RotationRpm { get => _rotationRpm; set => SetField(ref _rotationRpm, value); }

    /// <summary>Health code from the operating system: Healthy, Warning, Unhealthy, Unknown.</summary>
    public string Health { get => _health; set => SetField(ref _health, value); }

    /// <summary>SMART status code: OK, Warning, Failing, Unavailable.</summary>
    public string SmartStatus { get => _smartStatus; set => SetField(ref _smartStatus, value); }

    public double? TemperatureC
    {
        get => _temperatureC;
        set
        {
            if (!SetField(ref _temperatureC, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TemperatureText));
            OnPropertyChanged(nameof(IsTemperatureWarm));
            OnPropertyChanged(nameof(IsTemperatureHot));
        }
    }

    /// <summary>ATA power state code: Active, Idle, Standby, Sleep, Unknown.</summary>
    public string PowerState
    {
        get => _powerState;
        set
        {
            if (!SetField(ref _powerState, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowPowerState));
        }
    }

    public double? ActivityPercent { get => _activityPercent; set => SetField(ref _activityPercent, value); }

    public double? ReadBytesPerSec { get => _readBytesPerSec; set => SetField(ref _readBytesPerSec, value); }

    public double? WriteBytesPerSec { get => _writeBytesPerSec; set => SetField(ref _writeBytesPerSec, value); }

    public DateTime? SmartCapturedAtUtc { get => _smartCapturedAtUtc; set => SetField(ref _smartCapturedAtUtc, value); }

    public List<SmartAttributeStatus> SmartAttributes
    {
        get => _smartAttributes;
        set => SetField(ref _smartAttributes, value ?? new List<SmartAttributeStatus>());
    }


    [JsonIgnore]
    public string ModelText => string.IsNullOrWhiteSpace(Model) ? "—" : Model;

    [JsonIgnore]
    public string SizeText => FormatBytes(SizeBytes);

    [JsonIgnore]
    public string TemperatureText => TemperatureC.HasValue
        ? $"{TemperatureC.Value.ToString("0", CultureInfo.CurrentCulture)} °C"
        : "—";

    /// <summary>Warning colour threshold for the temperature readout.</summary>
    [JsonIgnore]
    public bool IsTemperatureWarm => TemperatureC.HasValue && TemperatureC.Value >= 40;

    /// <summary>Critical colour threshold for the temperature readout.</summary>
    [JsonIgnore]
    public bool IsTemperatureHot => TemperatureC.HasValue && TemperatureC.Value >= 50;

    [JsonIgnore]
    public string HealthPercentText => HealthPercent.HasValue
        ? $"{HealthPercent.Value.ToString(CultureInfo.CurrentCulture)} %"
        : string.Empty;

    [JsonIgnore]
    public bool HasHealthPercent => HealthPercent.HasValue;

    [JsonIgnore]
    public bool HasDriveLetters => !string.IsNullOrWhiteSpace(DriveLetters);

    [JsonIgnore]
    public string ActivityText => ActivityPercent.HasValue
        ? $"{ActivityPercent.Value.ToString("0", CultureInfo.CurrentCulture)} %"
        : "—";

    [JsonIgnore]
    public string ReadRateText => FormatRate(ReadBytesPerSec);

    [JsonIgnore]
    public string WriteRateText => FormatRate(WriteBytesPerSec);

    /// <summary>
    /// Only rotating SATA media with a known ATA power state expose a meaningful
    /// "are the platters spinning" row. When the controller/USB bridge blocks
    /// ATA pass-through the row is hidden instead of showing "Unknown / 0 %".
    /// </summary>
    [JsonIgnore]
    public bool ShowPowerState => IsHdd && IsSata &&
        !string.Equals(PowerState, DiskPowerStateCodes.Unknown, StringComparison.Ordinal);

    [JsonIgnore]
    public bool HasSmart => SmartAttributes.Count > 0;

    [JsonIgnore]
    public bool HasNoSmart => SmartAttributes.Count == 0;

    public void CopyFrom(PhysicalDiskStatus other)
    {
        var changed = false;
        changed |= SetField(ref _index, other.Index, nameof(Index));
        changed |= SetField(ref _model, other.Model, nameof(Model));
        changed |= SetField(ref _serial, other.Serial, nameof(Serial));
        changed |= SetField(ref _firmware, other.Firmware, nameof(Firmware));
        changed |= SetField(ref _driveLetters, other.DriveLetters, nameof(DriveLetters));
        changed |= SetField(ref _healthPercent, other.HealthPercent, nameof(HealthPercent));
        changed |= SetField(ref _sizeBytes, other.SizeBytes, nameof(SizeBytes));
        changed |= SetField(ref _busType, other.BusType, nameof(BusType));
        changed |= SetField(ref _mediaType, other.MediaType, nameof(MediaType));
        changed |= SetField(ref _isHdd, other.IsHdd, nameof(IsHdd));
        changed |= SetField(ref _isSata, other.IsSata, nameof(IsSata));
        changed |= SetField(ref _rotationRpm, other.RotationRpm, nameof(RotationRpm));
        changed |= SetField(ref _health, other.Health, nameof(Health));
        changed |= SetField(ref _smartStatus, other.SmartStatus, nameof(SmartStatus));
        changed |= SetField(ref _temperatureC, other.TemperatureC, nameof(TemperatureC));
        changed |= SetField(ref _powerState, other.PowerState, nameof(PowerState));
        changed |= SetField(ref _activityPercent, other.ActivityPercent, nameof(ActivityPercent));
        changed |= SetField(ref _readBytesPerSec, other.ReadBytesPerSec, nameof(ReadBytesPerSec));
        changed |= SetField(ref _writeBytesPerSec, other.WriteBytesPerSec, nameof(WriteBytesPerSec));
        changed |= SetField(ref _smartCapturedAtUtc, other.SmartCapturedAtUtc, nameof(SmartCapturedAtUtc));
        changed |= SetField(ref _smartAttributes, other.SmartAttributes, nameof(SmartAttributes));

        if (!changed)
        {
            return;
        }

        OnPropertyChanged(nameof(ModelText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(TemperatureText));
        OnPropertyChanged(nameof(IsTemperatureWarm));
        OnPropertyChanged(nameof(IsTemperatureHot));
        OnPropertyChanged(nameof(HealthPercentText));
        OnPropertyChanged(nameof(HasHealthPercent));
        OnPropertyChanged(nameof(HasDriveLetters));
        OnPropertyChanged(nameof(ActivityText));
        OnPropertyChanged(nameof(ReadRateText));
        OnPropertyChanged(nameof(WriteRateText));
        OnPropertyChanged(nameof(ShowPowerState));
        OnPropertyChanged(nameof(HasSmart));
        OnPropertyChanged(nameof(HasNoSmart));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "—";
        }

        const double gb = 1024d * 1024d * 1024d;
        const double tb = gb * 1024d;
        return bytes >= tb
            ? $"{(bytes / tb).ToString("0.#", CultureInfo.CurrentCulture)} TB"
            : $"{(bytes / gb).ToString("0.#", CultureInfo.CurrentCulture)} GB";
    }

    private static string FormatRate(double? bytesPerSecond)
    {
        if (!bytesPerSecond.HasValue || bytesPerSecond.Value < 0)
        {
            return "—";
        }

        var value = bytesPerSecond.Value;
        if (value >= 1024d * 1024d)
        {
            return $"{(value / (1024d * 1024d)).ToString("0.#", CultureInfo.CurrentCulture)} MB/s";
        }

        if (value >= 1024d)
        {
            return $"{(value / 1024d).ToString("0", CultureInfo.CurrentCulture)} KB/s";
        }

        return $"{value.ToString("0", CultureInfo.CurrentCulture)} B/s";
    }
}

/// <summary>Language-neutral health codes from the OS storage stack.</summary>
public static class DiskHealthCodes
{
    public const string Healthy = "Healthy";
    public const string Warning = "Warning";
    public const string Unhealthy = "Unhealthy";
    public const string Unknown = "Unknown";
}

/// <summary>Language-neutral SMART summary codes.</summary>
public static class DiskSmartStatusCodes
{
    public const string Ok = "OK";
    public const string Warning = "Warning";
    public const string Failing = "Failing";
    public const string Unavailable = "Unavailable";
}

/// <summary>Language-neutral media type codes.</summary>
public static class DiskMediaTypeCodes
{
    public const string Ssd = "SSD";
    public const string Hdd = "HDD";
    public const string Unknown = "Unknown";
}

/// <summary>Language-neutral bus type codes.</summary>
public static class DiskBusTypes
{
    public const string Sata = "SATA";
    public const string Ata = "ATA";
    public const string Nvme = "NVMe";
    public const string Usb = "USB";
    public const string Raid = "RAID";
    public const string Sas = "SAS";
    public const string Unknown = "Unknown";
}

/// <summary>Language-neutral ATA power state codes.</summary>
public static class DiskPowerStateCodes
{
    public const string Active = "Active";
    public const string Idle = "Idle";
    public const string Standby = "Standby";
    public const string Sleep = "Sleep";
    public const string Unknown = "Unknown";
}

