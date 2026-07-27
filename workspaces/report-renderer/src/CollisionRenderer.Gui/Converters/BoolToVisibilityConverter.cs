using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CollisionRenderer.Gui.Converters;

/// <summary>
/// Maps a bool to <see cref="Visibility"/>. Pass ConverterParameter="invert" to flip
/// the mapping (true =&gt; Collapsed).
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility v && v == Visibility.Visible;
}
