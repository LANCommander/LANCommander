using System.Linq;

namespace LANCommander.SDK.Extensions
{
    public static class EnumExtensions
    {
        public static bool IsIn<T>(this T value, params T[] values)
        {
            return values.Contains(value);
        }
    }
}
