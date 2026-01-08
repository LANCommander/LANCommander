export class TimeProvider
{
    public static GetLocalTimeZone(): string
    {
        const options = Intl.DateTimeFormat().resolvedOptions();
        
        return options.timeZone;
    }
}