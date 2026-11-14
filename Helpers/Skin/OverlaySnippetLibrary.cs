using System;
using System.Collections.Generic;
using System.Linq;
using IconGrid.Models;

namespace IconGrid.Helpers.Skin
{
    /// <summary>One ready-made code snippet offered in the Skin Studio layer panel.</summary>
    public sealed class OverlaySnippet
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;

        /// <summary>Block type the snippet is meant for ("gauge", "stat"...), or "any".</summary>
        public string BlockType { get; init; } = "any";

        /// <summary>JSON property patch applied to the module.</summary>
        public string Code { get; init; } = "{}";
    }

    /// <summary>
    /// Built-in snippet library for the Skin Studio. A snippet is a plain JSON property patch for
    /// one module (props of the blueprint block), so inserting one can never run code — it only
    /// changes the same data the properties panel writes.
    /// </summary>
    public static class OverlaySnippetLibrary
    {
        public static IReadOnlyList<OverlaySnippet> All { get; } = new List<OverlaySnippet>
        {
            new()
            {
                Id = "gauge-bar",
                Title = "Vertical bar",
                BlockType = "gauge",
                Code = "{\n  \"meter\": \"bar\"\n}"
            },
            new()
            {
                Id = "gauge-ring",
                Title = "Ring + percentage",
                BlockType = "gauge",
                Code = "{\n  \"meter\": \"ring\",\n  \"showPercent\": \"true\",\n  \"hasGrid\": \"false\"\n}"
            },
            new()
            {
                Id = "gauge-segments",
                Title = "Segments + temperature",
                BlockType = "gauge",
                Code = "{\n  \"meter\": \"segments\",\n  \"meterWidth\": \"56\",\n  \"meterHeight\": \"10\",\n  \"showTemp\": \"true\",\n  \"showPercent\": \"true\"\n}"
            },
            new()
            {
                Id = "gauge-text",
                Title = "Text only",
                BlockType = "gauge",
                Code = "{\n  \"meter\": \"text\",\n  \"showTemp\": \"false\"\n}"
            },
            new()
            {
                Id = "gauge-wide",
                Title = "Wide bar (16 x 20)",
                BlockType = "gauge",
                Code = "{\n  \"meter\": \"bar\",\n  \"meterWidth\": \"16\",\n  \"meterHeight\": \"20\"\n}"
            },
            new()
            {
                Id = "gauge-purple",
                Title = "Neon purple ring",
                BlockType = "gauge",
                Code = "{\n  \"meter\": \"ring\",\n  \"meterColor\": \"#A855F7\",\n  \"showPercent\": \"true\"\n}"
            },
            new()
            {
                Id = "usage-ring",
                Title = "Usage ring + percentage",
                BlockType = "usage",
                Code = "{\n  \"meter\": \"ring\",\n  \"showPercent\": \"true\"\n}"
            },
            new()
            {
                Id = "usage-segments",
                Title = "Usage segments",
                BlockType = "usage",
                Code = "{\n  \"meter\": \"segments\",\n  \"meterWidth\": \"56\",\n  \"showPercent\": \"true\"\n}"
            },
            new()
            {
                Id = "temp-colour",
                Title = "Warm temperature colour",
                BlockType = "temp",
                Code = "{\n  \"color\": \"#F87171\"\n}"
            },
            new()
            {
                Id = "ping-short",
                Title = "Short network label",
                BlockType = "ping",
                Code = "{\n  \"label\": \"Net\",\n  \"showDot\": \"true\"\n}"
            },
            new()
            {
                Id = "ping-nodot",
                Title = "Network without dot",
                BlockType = "ping",
                Code = "{\n  \"showDot\": \"false\"\n}"
            },
            new()
            {
                Id = "stat-fps",
                Title = "FPS with highlight",
                BlockType = "stat",
                Code = "{\n  \"label\": \"FPS\",\n  \"highlight\": \"true\"\n}"
            },
            new()
            {
                Id = "any-reset",
                Title = "Clear hidden flag",
                BlockType = "any",
                Code = "{\n  \"hidden\": \"\"\n}"
            }
        };

        /// <summary>Snippets that make sense for the selected module, best match first.</summary>
        public static IReadOnlyList<OverlaySnippet> ForBlock(OverlayBlock? block)
        {
            if (block == null)
            {
                return Array.Empty<OverlaySnippet>();
            }

            return All
                .Where(snippet => string.Equals(snippet.BlockType, block.Block, StringComparison.OrdinalIgnoreCase))
                .Concat(All.Where(snippet => snippet.BlockType == "any"))
                .ToList();
        }
    }
}
