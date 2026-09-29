namespace LANCommander.Server.Services.Utilities;

public static class ProcessCpuSampler
{
    /// <summary>
    /// CPU usage over a sampling window as a percentage of the whole machine (0–100):
    /// processor time consumed ÷ wall time elapsed ÷ logical processor count.
    /// </summary>
    /// <returns>Null when the window is empty or the inputs are nonsensical.</returns>
    public static double? ComputeCpuPercent(TimeSpan processorTimeDelta, TimeSpan elapsed, int processorCount)
    {
        if (elapsed <= TimeSpan.Zero || processorCount <= 0 || processorTimeDelta < TimeSpan.Zero)
            return null;

        var percent = processorTimeDelta.TotalMilliseconds / elapsed.TotalMilliseconds / processorCount * 100d;

        return Math.Clamp(percent, 0d, 100d);
    }
}
