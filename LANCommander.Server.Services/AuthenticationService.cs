using JetBrains.Annotations;
using LANCommander.Server.Models;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace LANCommander.Server.Services
{
    public class AuthenticationService : BaseService
    {
        public AuthenticationService(ILogger<AuthenticationService> logger) : base(logger)
        {
        }

        public static async Task<List<ExternalProvider>> GetExternalProviderTemplatesAsync()
        {
            var files = Directory.GetFiles(@"Templates/AuthenticationProviders", "*.yml", SearchOption.AllDirectories);

            var externalProviders = new List<ExternalProvider>();


            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            foreach (var file in files)
            {
                try
                {
                    var contents = await File.ReadAllTextAsync(file);

                    externalProviders.Add(deserializer.Deserialize<ExternalProvider>(contents));
                }
                catch { }
            }

            return externalProviders;
        }
    }
}
