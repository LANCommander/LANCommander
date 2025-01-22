using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class ScriptService : BaseDatabaseService<Script>
    {
        public ScriptService(
            ILogger<ScriptService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory) : base(logger, cache, repositoryFactory) { }

        public static IEnumerable<Snippet> GetSnippets()
        {
            var files = Directory.GetFiles(@"Snippets", "*.ps1", SearchOption.AllDirectories);

            return files.Select(f =>
            {
                var split = f.Split(Path.DirectorySeparatorChar);

                return new Snippet()
                {
                    Name = Path.GetFileNameWithoutExtension(f),
                    Group = split[1],
                    Content = File.ReadAllText(f)
                };
            });
        }
    }
}
