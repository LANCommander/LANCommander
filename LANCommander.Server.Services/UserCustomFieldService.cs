using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class UserCustomFieldService : BaseDatabaseService<UserCustomField>
    {

        public UserCustomFieldService(
            ILogger<UserCustomFieldService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory) : base(logger, cache, repositoryFactory) { }

        public async Task<UserCustomField> GetAsync(Guid userId, string name)
        {
            return await Repository.FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Name == name);
        }

        public async Task UpdateAsync(Guid userId, string name, string value)
        {
            if (name.Length > 64)
                throw new ArgumentException("Field name must be 64 characters or shorter");

            if (value.Length > 1024)
                throw new ArgumentException("Field value must be 1024 characters or less");

            var existing = await Repository.FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Name == name);

            if (existing.Value == value)
                return;

            if (existing == null)
            {
                await Repository.AddAsync(new UserCustomField
                {
                    Name = name,
                    Value = value
                });

                await Repository.SaveChangesAsync();
            }
            else if (!String.IsNullOrWhiteSpace(value))
            {
                existing.Value = value;

                await Repository.UpdateAsync(existing);

                await Repository.SaveChangesAsync();
            }
            else
            {
                await DeleteAsync(userId, name);
            }
        }

        public async Task DeleteAsync(Guid userId, string name)
        {
            var existing = await Repository.FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Name == name);

            Repository.Delete(existing);

            await Repository.SaveChangesAsync();
        }
    }
}
