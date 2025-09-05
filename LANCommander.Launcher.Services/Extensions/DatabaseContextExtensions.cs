using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;

namespace LANCommander.Launcher.Services.Extensions;

public static class DatabaseContextExtensions
{
    public static BulkImportBuilder<TModel, TKeyedModel> BulkImport<TModel, TKeyedModel>(
        this DatabaseContext databaseContext)
        where TModel : BaseModel, new()
        where TKeyedModel : SDK.Models.IKeyedModel
    {
        return new BulkImportBuilder<TModel, TKeyedModel>(databaseContext);
    }
}