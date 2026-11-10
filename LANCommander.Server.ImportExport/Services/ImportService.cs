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

        // Each context keeps its own events and the service-wide ones only receive a copy. Handing the
        // context the service's own handlers would let every subscriber (an open import dialog, say)
        // hear every other import on the server, and keep it alive for as long as the server runs.
        context.OnImportStarted.EventRaised += OnImportStarted.InvokeAsync;
        context.OnImportComplete.EventRaised += OnImportComplete.InvokeAsync;
        context.OnImportStatusUpdate.EventRaised += OnImportStatusUpdate.InvokeAsync;
        context.OnImportError.EventRaised += OnImportError.InvokeAsync;

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
