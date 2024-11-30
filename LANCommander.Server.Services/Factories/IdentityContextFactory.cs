using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Services.Factories
{
    public class CustomUserStore : UserStore<User, Role, DatabaseContext, Guid>
    {
        public CustomUserStore(DatabaseContext context) : base(context)
        {
        }
    }

    public class CustomRoleStore : RoleStore<Role, DatabaseContext, Guid>
    {
        public CustomRoleStore(DatabaseContext context) : base(context)
        {
        }
    }

    public class IdentityContext : IDisposable
    {
        public readonly DatabaseContext DatabaseContext;
        public readonly UserManager<User> UserManager;
        public readonly RoleManager<Role> RoleManager;

        public IdentityContext(DatabaseContext databaseContext, IServiceProvider serviceProvider)
        {
            DatabaseContext = databaseContext;

            var userStore = new CustomUserStore(DatabaseContext);
            UserManager = new UserManager<User>(
                userStore,
                serviceProvider.GetRequiredService<IOptions<IdentityOptions>>(),
                serviceProvider.GetRequiredService<IPasswordHasher<User>>(),
                serviceProvider.GetRequiredService<IEnumerable<IUserValidator<User>>>(),
                serviceProvider.GetRequiredService<IEnumerable<IPasswordValidator<User>>>(),
                serviceProvider.GetRequiredService<ILookupNormalizer>(),
                serviceProvider.GetRequiredService<IdentityErrorDescriber>(),
                serviceProvider.GetRequiredService<IServiceProvider>(),
                serviceProvider.GetRequiredService<ILogger<UserManager<User>>>());

            var roleStore = new CustomRoleStore(DatabaseContext);
            RoleManager = new RoleManager<Role>(
                roleStore,
                serviceProvider.GetRequiredService<IEnumerable<IRoleValidator<Role>>>(),
                serviceProvider.GetRequiredService<ILookupNormalizer>(),
                serviceProvider.GetRequiredService<IdentityErrorDescriber>(),
                serviceProvider.GetRequiredService<ILogger<RoleManager<Role>>>());
        }

        public void Dispose()
        {
            DatabaseContext.Dispose();
        }
    }
    public class IdentityContextFactory
    {
        private readonly DatabaseContext DatabaseContext;
        private readonly IServiceProvider ServiceProvider;

        public IdentityContextFactory(DatabaseContext databaseContext, IServiceProvider serviceProvider)
        {
            DatabaseContext = databaseContext;
            ServiceProvider = serviceProvider;
        }

        public IdentityContext Create()
        {
            return new IdentityContext(DatabaseContext, ServiceProvider);
        }
    }
}
