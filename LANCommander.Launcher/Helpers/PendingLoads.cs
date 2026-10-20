using System.Threading;

namespace LANCommander.Launcher.Helpers;

/// <summary>
/// Counts image loads that have started but not reached the screen yet. The app never waits on it;
/// debug fixtures do, so a screenshot is not taken while covers are still decoding.
/// </summary>
internal static class PendingLoads
{
    private static int _count;

    public static bool Any => Volatile.Read(ref _count) > 0;

    public static void Begin() => Interlocked.Increment(ref _count);

    public static void End() => Interlocked.Decrement(ref _count);
}
