namespace LANCommander.Server.ImportExport.Exceptions;

public class ExportSkippedException<TRecord> : Exception where TRecord : class
{
    public TRecord Record { get; set; }

    public ExportSkippedException(TRecord record, string message) : base(message)
    {
        Record = record;
    }

    public ExportSkippedException(TRecord record, string message, Exception innerException) : base(message,
        innerException)
    {
        Record = record;
    }
}