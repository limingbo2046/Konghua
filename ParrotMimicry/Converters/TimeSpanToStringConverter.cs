using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParrotMimicry
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TimeSpan displayValue)
            {
                return displayValue.ToString("c");
            }
            return ""; // 默认颜色
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // 这里一般不需要转换回去
        }
    }
}
