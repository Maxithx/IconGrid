using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;

namespace IconGrid.Helpers.Skin
{
    /// <summary>
    /// Niveau B of the Skin Studio snippet system: turns user-entered XAML into a DataTemplate
    /// for one overlay module.
    ///
    /// Safety model: the snippet is only ever parsed by <see cref="XamlReader"/>, which creates UI
    /// elements and can never execute C#. A few constructs that could load external types are
    /// rejected outright, the length is capped, and every failure returns null so the caller can
    /// fall back to the built-in template instead of breaking the overlay.
    ///
    /// The DataContext of the produced template is the <c>OverlayBlock</c> itself; telemetry and
    /// skin values are reached through the hosting ItemsControl (see <see cref="StarterExample"/>).
    /// </summary>
    public static class OverlaySnippetTemplateFactory
    {
        private const int MaxLength = 6000;
        private const string WpfNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        private static readonly string[] BlockedFragments =
        {
            "x:Class",
            "x:Code",
            "ObjectDataProvider",
            "Assembly=",
            "EventSetter",
            "Process.Start"
        };

        private static readonly Dictionary<string, DataTemplate?> Cache = new(StringComparer.Ordinal);
        private static readonly object Gate = new();

        /// <summary>Ready-to-edit example shown by the "Insert example" button in the studio.</summary>
        public const string StarterExample =
            "<Border Background=\"#14FFFFFF\" CornerRadius=\"3\" Padding=\"6,1\" Margin=\"0,0,8,0\">\n" +
            "    <StackPanel Orientation=\"Horizontal\">\n" +
            "        <TextBlock Text=\"{Binding DisplayLabel}\" Foreground=\"#00FF66\" FontWeight=\"Bold\" />\n" +
            "        <TextBlock Text=\" \" />\n" +
            "        <TextBlock Text=\"{Binding DataContext.SystemMonitor.CpuTemp, RelativeSource={RelativeSource AncestorType=ItemsControl}}\"\n" +
            "                   Foreground=\"#C9D1D9\" />\n" +
            "    </StackPanel>\n" +
            "</Border>";

        /// <summary>
        /// Parses <paramref name="xaml"/> into a DataTemplate. Returns false plus a short error key
        /// ("empty", "too-long", "blocked", "invalid") when the snippet cannot be used.
        /// </summary>
        public static bool TryBuild(string? xaml, out DataTemplate? template, out string error)
        {
            template = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(xaml))
            {
                error = "empty";
                return false;
            }

            var text = xaml.Trim();
            if (text.Length > MaxLength)
            {
                error = "too-long";
                return false;
            }

            foreach (var blocked in BlockedFragments)
            {
                if (text.IndexOf(blocked, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = "blocked";
                    return false;
                }
            }

            lock (Gate)
            {
                if (Cache.TryGetValue(text, out var cached))
                {
                    template = cached;
                    if (template == null)
                    {
                        error = "invalid";
                    }

                    return template != null;
                }
            }

            template = Parse(text);

            lock (Gate)
            {
                Cache[text] = template;
            }

            if (template == null)
            {
                error = "invalid";
                return false;
            }

            return true;
        }

        private static DataTemplate? Parse(string text)
        {
            try
            {
                if (text.StartsWith("<DataTemplate", StringComparison.OrdinalIgnoreCase))
                {
                    return XamlReader.Parse(EnsureNamespaces(text)) as DataTemplate;
                }

                var wrapped =
                    $"<DataTemplate xmlns=\"{WpfNamespace}\" xmlns:x=\"{XamlNamespace}\">{text}</DataTemplate>";
                return XamlReader.Parse(wrapped) as DataTemplate;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Adds the WPF/XAML namespaces to a pasted full DataTemplate that omits them.</summary>
        private static string EnsureNamespaces(string text)
        {
            if (text.IndexOf("xmlns=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }

            var insertAt = text.IndexOf('>', StringComparison.Ordinal);
            if (insertAt < 0)
            {
                return text;
            }

            var namespaces = $" xmlns=\"{WpfNamespace}\" xmlns:x=\"{XamlNamespace}\"";
            return text.Insert(insertAt, namespaces);
        }
    }
}
