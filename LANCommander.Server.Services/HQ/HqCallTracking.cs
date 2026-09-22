namespace LANCommander.Server.Services.HQ;

/// <summary>
/// Feeds the outcome of live HQ requests back into <see cref="HqConnectionService"/>, so a revoked
/// or expired token is noticed the moment a real call fails rather than at the next poll.
/// </summary>
public static class HqCallTracking
{
    public static async Task<T> TrackAsync<T>(
        this HqConnectionService connection,
        Func<Task<T>> call)
    {
        try
        {
            var result = await call();

            connection.ReportSuccess();

            return result;
        }
        catch (Exception ex)
        {
            connection.ReportFailure(ex);

            throw;
        }
    }
}
