using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LANCommander.PlaynitePlugin.Converters
{
    public class NullVisibilityConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Hidden : Visibility.Visible;
        }
    }
}
