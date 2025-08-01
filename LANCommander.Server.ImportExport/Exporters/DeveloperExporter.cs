using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class DeveloperExporter(
    CompanyService companyService) : BaseExporter<Company, Data.Models.Company>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Company record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Developer,
            Name = record.Name,
        };
    }

    public override bool CanExport(Company record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Company> ExportAsync(Guid id)
    {
        return await companyService.GetAsync<Company>(id);
    }
} 