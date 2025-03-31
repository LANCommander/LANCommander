using LANCommander.SDK.Models.Manifest;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class RedistributableImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Redistributable, Data.Models.Redistributable>
{
    private readonly RedistributableService _redistributableService;
    private readonly UserService _userService = serviceProvider.GetService<UserService>();
    private readonly IMapper _mapper = serviceProvider.GetService<IMapper>();
    
    public async Task<Data.Models.Redistributable> AddAsync(Redistributable record)
    {
        var redistributable = _mapper.Map<Data.Models.Redistributable>(record);

        try
        {
            return await _redistributableService.AddAsync(redistributable);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Redistributable>(record,
                "An unknown error occurred while trying to add redistributable", ex);
        }
    }

    public async Task<Data.Models.Redistributable> UpdateAsync(Redistributable record)
    {
        var existing = await _redistributableService.FirstOrDefaultAsync(r => r.Id == record.Id || r.Name == record.Name);

        try
        {
            existing.Name = record.Name;
            existing.Description = record.Description;
            existing.Notes = record.Notes;
            existing.CreatedOn = record.CreatedOn;
            existing.CreatedBy = await _userService.GetAsync(record.CreatedBy);
            existing.UpdatedOn = record.UpdatedOn;
            existing.UpdatedBy = await _userService.GetAsync(record.UpdatedBy);

            existing = await _redistributableService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Redistributable>(record,
                "An unknown error occurred while trying to add redistributable", ex);
        }
    }

    public async Task<bool> ExistsAsync(Redistributable record)
    {
        return await _redistributableService.ExistsAsync(r => r.Id == record.Id || r.Name == record.Name);
    }
} 