using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace LANCommander.PlaynitePlugin.Converters
{
    public class ByteSizeConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var size = Convert.ToDouble(value);

            return ByteSizeLib.ByteSize.FromBytes(size).ToString();
        }
    }
}
