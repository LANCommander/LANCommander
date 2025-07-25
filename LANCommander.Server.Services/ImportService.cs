using LANCommander.Server.Services.Importers;

namespace LANCommander.Server.Services;

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

    public async Task ExportAsync(Guid contextId, Stream outputStream)
    {
        if (ImportContexts.TryGetValue(contextId, out var context))
        {
            await context.ExportQueueAsync(outputStream);
            
            ImportContexts.Remove(contextId);
        }
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}