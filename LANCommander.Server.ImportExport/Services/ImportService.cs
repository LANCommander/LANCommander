using LANCommander.Server.ImportExport.Importers;
using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Services;

public class ImportService : IDisposable
{
    public EventHandler<ImportStatusUpdate> OnImportStarted;
    public EventHandler<ImportStatusUpdate> OnImportComplete;
    public EventHandler<ImportStatusUpdate> OnImportStatusUpdate;
    public EventHandler<ImportStatusUpdate> OnImportError;
    
    private Dictionary<Guid, ImportContext> _importContexts = new();

    public Guid AddContext(ImportContext context)
    {
        var id = Guid.NewGuid();
        
        context.SetId(id);
        
        _importContexts.Add(id, context);
        
        context.OnImportStarted += OnImportStarted;
        context.OnImportComplete += OnImportComplete;
        context.OnImportStatusUpdate += OnImportStatusUpdate;
        context.OnImportError += OnImportError;

        return id;
    }

    public void RemoveContext(Guid id)
    {
        _importContexts.Remove(id);
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