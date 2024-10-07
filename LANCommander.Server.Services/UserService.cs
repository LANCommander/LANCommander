using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class UserService : BaseService
    {
        private readonly DatabaseContext DatabaseContext;
        private readonly UserManager<User> UserManager;

        public UserService(
            ILogger<UserService> logger,
            DatabaseContext databaseContext,
            UserManager<User> userManager) : base(logger)
        {
            DatabaseContext = databaseContext;
            UserManager = userManager;
        }

        public async Task<User> Get(Guid id)
        {
            return await UserManager.FindByIdAsync(id.ToString());
        }

        public async Task<User> Get(string username)
        {
            return await UserManager.FindByNameAsync(username);
        }

        public async Task<UserCustomField> GetCustomField(Guid userId, string name)
        {
            using (var repo = new Repository<UserCustomField>(DatabaseContext))
            {
                return repo.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);
            }
        }

        public async Task UpdateCustomField(Guid userId, string name, string value)
        {
            if (name.Length > 64)
                throw new ArgumentException("Field name must be 64 characters or shorter");

            if (value.Length > 1024)
                throw new ArgumentException("Field value must be 1024 characters or less");

            using (var repo = new Repository<UserCustomField>(DatabaseContext))
            {
                var existing = repo.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);

                if (existing.Value == value)
                    return;

                if (existing == null)
                {
                    await repo.Add(new UserCustomField
                    {
                        Name = name,
                        Value = value
                    });

                    await repo.SaveChanges();
                }
                else if (!String.IsNullOrWhiteSpace(value))
                {
                    existing.Value = value;

                    repo.Update(existing);

                    await repo.SaveChanges();
                }
                else
                {
                    await DeleteCustomField(userId, name);
                }
            }
        }

        public async Task DeleteCustomField(Guid userId, string name)
        {
            using (var repo = new Repository<UserCustomField>(DatabaseContext))
            {
                var existing = repo.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);

                repo.Delete(existing);
                await repo.SaveChanges();
            }
        }

        public async Task DeleteCustomField(Guid userId, Guid id)
        {
            using (var repo = new Repository<UserCustomField>(DatabaseContext))
            {
                var existing = repo.FirstOrDefault(cf => cf.UserId == userId && cf.Id == id);

                repo.Delete(existing);
                await repo.SaveChanges();
            }
        }
    }
}
