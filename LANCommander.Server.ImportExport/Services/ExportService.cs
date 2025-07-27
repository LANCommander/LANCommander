using LANCommander.Server.ImportExport.Importers;

namespace LANCommander.Server.ImportExport.Services;

public class ExportService : IDisposable
{
    private Dictionary<Guid, ExportContext> ExportContexts = new();

    public Guid EnqueueContext(ExportContext context)
    {
        var id = Guid.NewGuid();
        
        ExportContexts.Add(id, context);

        return id;
    }

    public ExportContext GetContext(Guid id)
    {
        if (ExportContexts.TryGetValue(id, out var context))
            return context;
        
        return null;
    }

    public async Task ExportAsync(Guid contextId, Stream outputStream)
    {
        if (ExportContexts.TryGetValue(contextId, out var context))
        {
            await context.ExportQueueAsync(outputStream);
            
            ExportContexts.Remove(contextId);
        }
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}