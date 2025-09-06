export class TimeProvider
{
    GetLocalTimeZone(): string
    {
        const options = Intl.DateTimeFormat().resolvedOptions();
        
        return options.timeZone;
    }
}