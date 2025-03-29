using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using System.Globalization;

namespace ParrotMimicry
{

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? Colors.YellowGreen : Colors.Gray; // 当 IsActive 为 true 时，返回红色，否则返回灰色
            }
            return Colors.Transparent; // 默认颜色
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // 这里一般不需要转换回去
        }
    }


}
