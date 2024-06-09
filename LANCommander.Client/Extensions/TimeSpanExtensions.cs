using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Extensions
{
    public static class TimeSpanExtensions
    {
        public static string ToShortTime(this TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString("hh:mm:ss");
            else
                return timeSpan.ToString("mm:ss");
        }
    }
}
