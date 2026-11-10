namespace LANCommander.Server.ImportExport.Exceptions;

/// <summary>
/// Thrown by an importer for a record that can never be imported, e.g. a save whose owner doesn't
/// exist on this server. The record is left out and reported; the rest of the import carries on.
/// </summary>
public abstract class ImportSkippedException : Exception
{
    protected ImportSkippedException(string message) : base(message)
    {
    }

    protected ImportSkippedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ImportSkippedException<TRecord> : ImportSkippedException where TRecord : class
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
