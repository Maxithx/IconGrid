using System;
using System.Collections.Generic;
using System.Linq;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// One curated desktop NVIDIA GPU entry. The values are used for display and
/// conservative safety hints, never for writing clocks by themselves.
/// </summary>
public sealed record NvidiaGpuSpec(
    string MatchToken,
    string Architecture,
    string Generation,
    int VramGb,
    string MemoryType,
    int BusWidthBits,
    int BaseClockMhz,
    int BoostClockMhz,
    int TdpWatts,
    int SafeCoreOffsetMhz,
    int SafeMemoryOffsetMhz)
{
    public string DisplayName => MatchToken;

    public string SpecSummary
    {
        get
        {
            if (VramGb <= 0)
            {
                return $"{Architecture} · {Generation}";
            }

            var parts = new List<string>
            {
                Architecture,
                Generation,
                $"{VramGb} GB {MemoryType}",
                $"{BusWidthBits}-bit",
                $"{BaseClockMhz}–{BoostClockMhz} MHz",
                $"{TdpWatts} W TDP"
            };

            return string.Join(" · ", parts);
        }
    }
}

/// <summary>
/// Small curated catalog of common desktop GTX/RTX cards. Matching is by model
/// token in the WMI name, with the longest token first so "RTX 4070 SUPER"
/// wins over "RTX 4070". Unknown models fall back to the generic conservative
/// limits already used by the tuning UI.
/// </summary>
public static class NvidiaGpuCatalog
{
    private static readonly NvidiaGpuSpec[] Specs =
    {
        // RTX 50 (Blackwell)
        New("RTX 5090", "Blackwell", "RTX 50 series", 32, "GDDR7", 512, 2017, 2407, 575),
        New("RTX 5080", "Blackwell", "RTX 50 series", 16, "GDDR7", 256, 2295, 2617, 360),
        New("RTX 5070 TI", "Blackwell", "RTX 50 series", 16, "GDDR7", 256, 2300, 2452, 300),
        New("RTX 5070", "Blackwell", "RTX 50 series", 12, "GDDR7", 192, 2160, 2512, 250),
        New("RTX 5060 TI", "Blackwell", "RTX 50 series", 16, "GDDR7", 128, 2407, 2572, 180),
        New("RTX 5060", "Blackwell", "RTX 50 series", 8, "GDDR7", 128, 2280, 2497, 145),

        // RTX 40 (Ada)
        New("RTX 4090", "Ada Lovelace", "RTX 40 series", 24, "GDDR6X", 384, 2235, 2520, 450),
        New("RTX 4080 SUPER", "Ada Lovelace", "RTX 40 series", 16, "GDDR6X", 256, 2210, 2550, 320),
        New("RTX 4080", "Ada Lovelace", "RTX 40 series", 16, "GDDR6X", 256, 2205, 2505, 320),
        New("RTX 4070 TI SUPER", "Ada Lovelace", "RTX 40 series", 16, "GDDR6X", 256, 2340, 2610, 285),
        New("RTX 4070 TI", "Ada Lovelace", "RTX 40 series", 12, "GDDR6X", 192, 2310, 2610, 285),
        New("RTX 4070 SUPER", "Ada Lovelace", "RTX 40 series", 12, "GDDR6X", 192, 1980, 2475, 220),
        New("RTX 4070", "Ada Lovelace", "RTX 40 series", 12, "GDDR6X", 192, 1920, 2475, 200),
        New("RTX 4060 TI", "Ada Lovelace", "RTX 40 series", 8, "GDDR6", 128, 2310, 2535, 160),
        New("RTX 4060", "Ada Lovelace", "RTX 40 series", 8, "GDDR6", 128, 1830, 2460, 115),

        // RTX 30 (Ampere)
        New("RTX 3090 TI", "Ampere", "RTX 30 series", 24, "GDDR6X", 384, 1560, 1860, 450),
        New("RTX 3090", "Ampere", "RTX 30 series", 24, "GDDR6X", 384, 1395, 1695, 350),
        New("RTX 3080 TI", "Ampere", "RTX 30 series", 12, "GDDR6X", 384, 1370, 1665, 350),
        New("RTX 3080", "Ampere", "RTX 30 series", 10, "GDDR6X", 320, 1440, 1710, 320),
        New("RTX 3070 TI", "Ampere", "RTX 30 series", 8, "GDDR6X", 256, 1575, 1770, 290),
        New("RTX 3070", "Ampere", "RTX 30 series", 8, "GDDR6", 256, 1500, 1725, 220),
        New("RTX 3060 TI", "Ampere", "RTX 30 series", 8, "GDDR6", 256, 1410, 1665, 200),
        New("RTX 3060", "Ampere", "RTX 30 series", 12, "GDDR6", 192, 1320, 1777, 170),
        New("RTX 3050", "Ampere", "RTX 30 series", 8, "GDDR6", 128, 1552, 1777, 130),

        // RTX 20 (Turing)
        New("RTX 2080 TI", "Turing", "RTX 20 series", 11, "GDDR6", 352, 1350, 1545, 250),
        New("RTX 2080 SUPER", "Turing", "RTX 20 series", 8, "GDDR6", 256, 1650, 1815, 250),
        New("RTX 2080", "Turing", "RTX 20 series", 8, "GDDR6", 256, 1515, 1710, 215),
        New("RTX 2070 SUPER", "Turing", "RTX 20 series", 8, "GDDR6", 256, 1605, 1770, 215),
        New("RTX 2070", "Turing", "RTX 20 series", 8, "GDDR6", 256, 1410, 1620, 175),
        New("RTX 2060 SUPER", "Turing", "RTX 20 series", 8, "GDDR6", 256, 1470, 1650, 175),
        New("RTX 2060", "Turing", "RTX 20 series", 6, "GDDR6", 192, 1365, 1680, 160),

        // GTX 16 (Turing)
        New("GTX 1660 TI", "Turing", "GTX 16 series", 6, "GDDR6", 192, 1500, 1770, 120),
        New("GTX 1660 SUPER", "Turing", "GTX 16 series", 6, "GDDR6", 192, 1530, 1785, 125),
        New("GTX 1660", "Turing", "GTX 16 series", 6, "GDDR5", 192, 1530, 1785, 120),
        New("GTX 1650 SUPER", "Turing", "GTX 16 series", 4, "GDDR6", 128, 1530, 1725, 100),
        New("GTX 1650", "Turing", "GTX 16 series", 4, "GDDR5", 128, 1485, 1665, 75),

        // GTX 10 (Pascal)
        New("GTX 1080 TI", "Pascal", "GTX 10 series", 11, "GDDR5X", 352, 1480, 1582, 250),
        New("GTX 1080", "Pascal", "GTX 10 series", 8, "GDDR5X", 256, 1607, 1733, 180),
        New("GTX 1070 TI", "Pascal", "GTX 10 series", 8, "GDDR5", 256, 1607, 1683, 180),
        New("GTX 1070", "Pascal", "GTX 10 series", 8, "GDDR5", 256, 1506, 1683, 150),
        New("GTX 1060", "Pascal", "GTX 10 series", 6, "GDDR5", 192, 1506, 1708, 120),
        New("GTX 1050 TI", "Pascal", "GTX 10 series", 4, "GDDR5", 128, 1290, 1392, 75),
        New("GTX 1050", "Pascal", "GTX 10 series", 2, "GDDR5", 128, 1354, 1455, 75)
    };

    private static readonly NvidiaGpuSpec[] OrderedSpecs =
        Specs.OrderByDescending(spec => spec.MatchToken.Length).ToArray();

    public static NvidiaGpuSpec? Match(string? gpuName)
    {
        if (string.IsNullOrWhiteSpace(gpuName))
        {
            return null;
        }

        var normalized = gpuName.ToUpperInvariant();
        return OrderedSpecs.FirstOrDefault(spec =>
            normalized.Contains(spec.MatchToken, StringComparison.Ordinal));
    }

    /// <summary>
    /// Architecture-only fallback for NVIDIA models that are not in the curated
    /// list. It deliberately leaves detailed specs and safe offsets empty so the
    /// tuning UI keeps its conservative unknown-model envelope.
    /// </summary>
    public static NvidiaGpuSpec? InferFamily(string? gpuName)
    {
        if (string.IsNullOrWhiteSpace(gpuName))
        {
            return null;
        }

        var normalized = gpuName.ToUpperInvariant();
        return normalized switch
        {
            _ when normalized.Contains("RTX 50", StringComparison.Ordinal) =>
                Family("Blackwell", "RTX 50 series"),
            _ when normalized.Contains("RTX 40", StringComparison.Ordinal) =>
                Family("Ada Lovelace", "RTX 40 series"),
            _ when normalized.Contains("RTX 30", StringComparison.Ordinal) =>
                Family("Ampere", "RTX 30 series"),
            _ when normalized.Contains("RTX 20", StringComparison.Ordinal) ||
                   normalized.Contains("GTX 16", StringComparison.Ordinal) =>
                Family("Turing", "RTX 20 / GTX 16 series"),
            _ when normalized.Contains("GTX 10", StringComparison.Ordinal) =>
                Family("Pascal", "GTX 10 series"),
            _ when normalized.Contains("GTX 9", StringComparison.Ordinal) =>
                Family("Maxwell", "GTX 900 series"),
            _ => null
        };
    }

    private static NvidiaGpuSpec Family(string architecture, string generation)
        => new(
            MatchToken: string.Empty,
            Architecture: architecture,
            Generation: generation,
            VramGb: 0,
            MemoryType: string.Empty,
            BusWidthBits: 0,
            BaseClockMhz: 0,
            BoostClockMhz: 0,
            TdpWatts: 0,
            SafeCoreOffsetMhz: 0,
            SafeMemoryOffsetMhz: 0);

    private static NvidiaGpuSpec New(
        string token,
        string architecture,
        string generation,
        int vramGb,
        string memoryType,
        int busWidthBits,
        int baseClockMhz,
        int boostClockMhz,
        int tdpWatts)
        => new(
            token,
            architecture,
            generation,
            vramGb,
            memoryType,
            busWidthBits,
            baseClockMhz,
            boostClockMhz,
            tdpWatts,
            SafeCoreOffsetMhz: 150,
            SafeMemoryOffsetMhz: 500);
}
