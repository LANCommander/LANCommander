using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class GenreExporter(
    GenreService genreService) : BaseExporter<Genre, Data.Models.Genre>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Genre record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Genre,
            Name = record.Name,
        };
    }

    public override bool CanExport(Genre record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Genre> ExportAsync(Guid id)
    {
        return await genreService.GetAsync<Genre>(id);
    }
} 