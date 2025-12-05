namespace LANCommander.Launcher.Services.Exceptions;

public class ImportSkippedException<TRecord> : Exception where TRecord : class
{
    public TRecord Record { get; set; }

    public ImportSkippedException(TRecord record, string message) : base(message)
    {
        Record = record;
    }

    public ImportSkippedException(TRecord record, string message, Exception innerException) : base(message,
        innerException)
    {
        Record = record;
    }
}