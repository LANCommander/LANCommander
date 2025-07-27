using LANCommander.Server.ImportExport.Importers;

namespace LANCommander.Server.ImportExport.Services;

public class ImportService : IDisposable
{
    private Dictionary<Guid, ImportContext> ImportContexts = new();

    public Guid EnqueueContext(ImportContext context)
    {
        var id = Guid.NewGuid();
        
        ImportContexts.Add(id, context);

        return id;
    }

    public ImportContext GetContext(Guid id)
    {
        if (ImportContexts.TryGetValue(id, out var context))
            return context;
        
        return null;
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}