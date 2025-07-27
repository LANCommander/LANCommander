using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class PublisherExporter(CompanyService companyService) : BaseExporter<Company, Data.Models.Company>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Company record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.Publishers,
            Name = record.Name,
        };
    }

    public override bool CanExport(Company record) => ExportContext.DataRecord is Data.Models.Company;

    public override async Task<Company> ExportAsync(Guid id)
    {
        return await companyService.GetAsync<Company>(id);
    }
} 