using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin.Helpers
{
    internal static class RetryHelper
    {
        internal static readonly ILogger Logger = LogManager.GetLogger();

        internal static T RetryOnException<T>(int maxAttempts, TimeSpan delay, T @default, Func<T> action)
        {
            int attempts = 0;

            do
            {
                try
                {
                    Logger.Trace($"Attempt #{attempts + 1}/{maxAttempts}...");

                    attempts++;
                    return action();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Attempt failed!");

                    if (attempts >= maxAttempts)
                        return @default;

                    Task.Delay(delay).Wait();
                }
            } while (true);
        }
    }
}
