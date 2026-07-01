using System.Windows;

namespace IconGrid.Helpers;

public static class DevInspector
{
    public static readonly DependencyProperty MetadataProperty =
        DependencyProperty.RegisterAttached(
            "Metadata",
            typeof(string),
            typeof(DevInspector),
            new PropertyMetadata(string.Empty));

    public static string GetMetadata(DependencyObject obj) =>
        (string)obj.GetValue(MetadataProperty);

    public static void SetMetadata(DependencyObject obj, string value) =>
        obj.SetValue(MetadataProperty, value);
}
