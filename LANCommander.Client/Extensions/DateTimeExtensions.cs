using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToRelativeDate(this DateTime dateTime)
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            if (dateTime.Day == today.Day && dateTime.Month == today.Month && dateTime.Year == today.Year)
                return "Today";
            else if (dateTime.Day == yesterday.Day && dateTime.Month == yesterday.Month && dateTime.Year == yesterday.Year)
                return "Yesterday";
            else
                return dateTime.ToString("MMMM d");
        }
    }
}
