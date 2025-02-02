using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class UserCustomFieldService(
        ILogger<UserCustomFieldService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<UserCustomField>(logger, cache, mapper, contextFactory)
    {
        public override async Task<UserCustomField> UpdateAsync(UserCustomField entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(ucf => ucf.User);
            });
        }

        public async Task<UserCustomField> GetAsync(Guid userId, string name)
        {
            return await FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Name == name);
        }

        public async Task UpdateAsync(Guid userId, string name, string value)
        {
            if (name.Length > 64)
                throw new ArgumentException("Field name must be 64 characters or shorter");

            if (value.Length > 1024)
                throw new ArgumentException("Field value must be 1024 characters or less");

            var existing = await GetAsync(userId, name);

            if (existing.Value == value)
                return;

            if (existing == null)
            {
                await AddAsync(new UserCustomField
                {
                    Name = name,
                    Value = value
                });
            }
            else if (!String.IsNullOrWhiteSpace(value))
            {
                existing.Value = value;

                await UpdateAsync(existing);
            }
            else
            {
                await DeleteAsync(userId, name);
            }
        }

        public async Task DeleteAsync(Guid userId, string name)
        {
            var existing = await GetAsync(userId, name);

            await DeleteAsync(existing);
        }
    }
}
