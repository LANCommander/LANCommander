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
            Repository<UserCustomField> repository) : base(logger, cache, repository) { }

        public async Task<UserCustomField> Get(Guid userId, string name)
        {
            return await Repository.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);
        }

        public async Task Update(Guid userId, string name, string value)
        {
            if (name.Length > 64)
                throw new ArgumentException("Field name must be 64 characters or shorter");

            if (value.Length > 1024)
                throw new ArgumentException("Field value must be 1024 characters or less");

            var existing = await Repository.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);

            if (existing.Value == value)
                return;

            if (existing == null)
            {
                await Repository.Add(new UserCustomField
                {
                    Name = name,
                    Value = value
                });

                await Repository.SaveChanges();
            }
            else if (!String.IsNullOrWhiteSpace(value))
            {
                existing.Value = value;

                Repository.Update(existing);

                await Repository.SaveChanges();
            }
            else
            {
                await Delete(userId, name);
            }
        }

        public async Task Delete(Guid userId, string name)
        {
            var existing = await Repository.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);

            Repository.Delete(existing);

            await Repository.SaveChanges();
        }
    }
}
