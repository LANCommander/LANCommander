using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Services
{
    public class UserService(
        ILogger<UserService> logger,
        DatabaseContext databaseContext,
        UserManager<User> userManager) : BaseService(logger)
    {
        public Task<User> Get(Guid id)
        {
            return userManager.FindByIdAsync(id.ToString());
        }

        public Task<User> Get(string username)
        {
            return userManager.FindByNameAsync(username);
        }

        public Task<UserCustomField> GetCustomField(Guid userId, string name)
        {
            return databaseContext.Set<UserCustomField>().FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Name == name);
        }

        public async Task UpdateCustomField(Guid userId, string name, string value)
        {
            if (name.Length > 64)
                throw new ArgumentException("Field name must be 64 characters or shorter");

            if (value.Length > 1024)
                throw new ArgumentException("Field value must be 1024 characters or less");


            var existing = await databaseContext.Set<UserCustomField>().FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Name == name);

            if (existing is not null && existing.Value == value)
                return;

            if (existing == null)
            {
                databaseContext.Set<UserCustomField>().Add(new UserCustomField
                {
                    Name = name,
                    Value = value
                });

                await databaseContext.SaveChangesAsync();
            }
            else if (!string.IsNullOrWhiteSpace(value))
            {
                existing.Value = value;

                databaseContext.Set<UserCustomField>().Update(existing);

                await databaseContext.SaveChangesAsync();
            }
            else
            {
                await DeleteCustomField(userId, name);
            }
        }

        public async Task DeleteCustomField(Guid userId, string name)
        {
            var existing = await databaseContext.Set<UserCustomField>().FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Name == name);

            if (existing is null) return;

            databaseContext.Set<UserCustomField>().Remove(existing);
            await databaseContext.SaveChangesAsync();
        }

        public async Task DeleteCustomField(Guid userId, Guid id)
        {
            var existing = await databaseContext.Set<UserCustomField>().FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Id == id);

            if (existing is null) return;

            databaseContext.Set<UserCustomField>().Remove(existing);
            await databaseContext.SaveChangesAsync();
        }
    }
}
