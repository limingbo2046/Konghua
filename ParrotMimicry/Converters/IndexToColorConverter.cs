using System;
using System.Globalization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;

namespace ParrotMimicry
{
    public class IndexToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index % 2 == 0 ? Colors.LightGray : Colors.LightBlue;
            }
            return Colors.LightBlue;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class IndexToButtonColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index % 2 == 0 ? Colors.LightBlue : Colors.LightGray;
            }
            return Colors.LightGray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}