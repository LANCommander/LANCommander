using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace LANCommander.PlaynitePlugin.Converters
{
    public class IEnumerableEmptyVisibilityConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<object>)
                return ((IEnumerable<object>)value).Count() == 0 ? Visibility.Visible : Visibility.Collapsed;
            else
                return Visibility.Visible;
        }
    }
}
