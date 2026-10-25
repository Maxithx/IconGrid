using System;
using System.Text.RegularExpressions;

namespace IconGrid.Helpers.Hardware.Storage;

/// <summary>
/// Last-resort SSD/HDD classification from the drive model string.
///
/// Used only when the OS reports an unspecified media type and ATA IDENTIFY word 217 is
/// unavailable (USB bridges, some storage drivers). The patterns are intentionally
/// conservative: an unknown model stays "Unknown" rather than guessing.
/// </summary>
public static class DiskModelClassifier
{
    private static readonly Regex[] SsdPatterns =
    {
        new(@"(?i)SSD"),                       // Any model advertising itself as an SSD
        new(@"(?i)\bMZ-"),                     // Samsung SSD (SATA + NVMe)
        new(@"(?i)\bWDS\d"),                   // WD SSD (WDS100T2B0A)
        new(@"(?i)\bWD\s+(Blue|Green|Red|Black)\s+SA\d"), // WD Blue SA510
        new(@"(?i)\bSA\d{3}"),                 // WD SA510 / Kingston SA400 series
        new(@"(?i)\bSDSS"),                    // SanDisk SSD
        new(@"(?i)\b(SV300|SUV\d{3}|SHFS|SKC|KC\d{4}|DC\d{4}|A\d{4}|SNV\d)"), // Kingston SSD series
        new(@"(?i)\bZA\d"),                    // Seagate SSD (BarraCuda/IronWolf SSD)
        new(@"(?i)\bTS\d+G"),                  // Transcend SSD
        new(@"(?i)\b(INTEL\s+SSD|SSDPEK|PLEXTOR|ADATA|XPG|CORSAIR|SANDISK|CRUCIAL)")
    };

    private static readonly Regex[] HddPatterns =
    {
        new(@"(?i)\bST\d{3,}"),                // Seagate HDD (ST1000LM024, ST4000DM004)
        new(@"(?i)\bWDC?\s*WD\d{3,}"),         // WDC WD5000AAKX-221CA0
        new(@"(?i)\bWD\d{2,}"),                // WD10EZEX, WD40EFRX
        new(@"(?i)\b(HD|HM|SP)\d{3,}"),        // Samsung / Hitachi desktop + mobile HDD
        new(@"(?i)\b(DT0|MG0|MQ0|MD0|HDWD)\d"),// Toshiba HDD
        new(@"(?i)\b(HDS|HUA|HTS|HCS|HMS|HCC)\d"), // Hitachi / HGST HDD
        new(@"(?i)\bHDD\b")
    };

    /// <summary>
    /// Returns <see cref="DiskMediaTypeCodes.Ssd"/>, <see cref="DiskMediaTypeCodes.Hdd"/>,
    /// or null when the model does not match a known family.
    /// </summary>
    public static string? Classify(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        var value = model.Trim();

        foreach (var pattern in SsdPatterns)
        {
            if (pattern.IsMatch(value))
            {
                return DiskMediaTypeCodes.Ssd;
            }
        }

        foreach (var pattern in HddPatterns)
        {
            if (pattern.IsMatch(value))
            {
                return DiskMediaTypeCodes.Hdd;
            }
        }

        return null;
    }
}
