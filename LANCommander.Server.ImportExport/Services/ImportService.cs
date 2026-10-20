using System.Collections.Concurrent;
using LANCommander.SDK;
using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Services;

public class ImportService : IDisposable
{
    public AsyncEventHandler<ImportStatusUpdate> OnImportStarted = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportComplete = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportStatusUpdate = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportError = new();

    // Concurrent because contexts are now created from API requests as well as Blazor
    // circuits, so several threads can add and remove at once.
    private readonly ConcurrentDictionary<Guid, ImportContext> _importContexts = new();

    public Guid AddContext(ImportContext context)
    {
        var id = Guid.NewGuid();
        
        context.SetId(id);
        
        _importContexts[id] = context;
        
        context.OnImportStarted = OnImportStarted;
        context.OnImportComplete = OnImportComplete;
        context.OnImportStatusUpdate = OnImportStatusUpdate;
        context.OnImportError = OnImportError;

        return id;
    }

    public void RemoveContext(Guid id)
    {
        _importContexts.TryRemove(id, out _);
    }

    public IEnumerable<ImportContext> GetContexts()
    {
        return _importContexts.Values;
    }

    public ImportContext GetContext(Guid id)
    {
        if (_importContexts.TryGetValue(id, out var context))
            return context;
        
        return null;
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}
