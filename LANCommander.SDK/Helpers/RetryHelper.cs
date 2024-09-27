using Microsoft.Extensions.Logging;
using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace LANCommander.SDK.Helpers
{
    internal static class RetryHelper
    {
        internal static readonly ILogger Logger;

        internal static T RetryOnException<T>(int maxAttempts, TimeSpan delay, T @default, Func<T> action)
        {
            int attempts = 0;

            do
            {
                try
                {
                    Logger?.LogTrace($"Attempt #{attempts + 1}/{maxAttempts}...");

                    attempts++;
                    return action();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"Attempt failed!");

                    if (attempts >= maxAttempts)
                        return @default;

                    Task.Delay(delay).Wait();
                }
            } while (true);
        }

        internal static async Task<T> RetryOnExceptionAsync<T>(int maxAttempts, TimeSpan delay, T @default, Func<Task<T>> action)
        {
            int attempts = 0;

            do
            {
                try
                {
                    Logger?.LogTrace($"Attempt #{attempts + 1}/{maxAttempts}...");

                    attempts++;
                    return await action();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"Attempt failed!");

                    if (attempts >= maxAttempts)
                        return @default;

                    Task.Delay(delay).Wait();
                }
            } while (true);
        }
    }
}
