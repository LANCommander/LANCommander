namespace LANCommander.UI.Providers;

public class LocalTimeProvider : TimeProvider
{
    private TimeZoneInfo? _localTimeZone;
    
    public event EventHandler? LocalTimeZoneChanged;
    
    public override TimeZoneInfo LocalTimeZone => _localTimeZone ?? base.LocalTimeZone;
    
    internal bool IsLocalTimeZoneSet => _localTimeZone != null;

    public void SetLocalTimeZone(string timeZone)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var timeZoneInfo))
            timeZoneInfo = null;

        if (timeZoneInfo != LocalTimeZone)
        {
            _localTimeZone = timeZoneInfo;
            
            LocalTimeZoneChanged?.Invoke(this, EventArgs.Empty);
        }
    }
 }