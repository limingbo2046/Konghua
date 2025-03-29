using Microsoft.Maui.Graphics;

namespace ParrotMimicry.Converters;

public class StringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string str)
        {
            return string.IsNullOrEmpty(str) ? Colors.Gray : Colors.Green;
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}