using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using Playnite.SDK;

namespace LANCommander.PlaynitePlugin.Converters
{
    public class RelativeDateConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime)
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var date = (DateTime)value;

                if (date.Day == today.Day && date.Month == today.Month && date.Year == today.Year)
                    return ResourceProvider.GetString("LOCLANCommanderToday") + " " + date.ToString("h:mm tt");
                else if (date.Day == yesterday.Day && date.Month == yesterday.Month && date.Year == yesterday.Year)
                    return ResourceProvider.GetString("LOCLANCommanderYesterday") + " " + date.ToString("h:mm tt");
                else
                    return date.ToString("MMM d h:mm tt");
            }
            else
                return "";
        }
    }
}
