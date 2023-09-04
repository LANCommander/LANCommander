using LANCommander.Data;
using LANCommander.Data.Enums;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Helpers;
using LANCommander.Models;
using LANCommander.SDK;
using System.Drawing;

namespace LANCommander.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        private readonly ArchiveService ArchiveService;

        public GameService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor, ArchiveService archiveService) : base(dbContext, httpContextAccessor)
        {
            ArchiveService = archiveService;
        }

        public override async Task Delete(Game game)
        {
            foreach (var archive in game.Archives.OrderByDescending(a => a.CreatedOn))
            {
                await ArchiveService.Delete(archive);

                FileHelpers.DeleteIfExists($"Icon/{game.Id}.png".ToPath());
            }

            await base.Delete(game);
        }

        public async Task<GameManifest> GetManifest(Guid id)
        {
            var game = await Get(id);

            if (game == null)
                return null;

            var manifest = new GameManifest()
            {
                Title = game.Title,
                SortTitle = game.SortTitle,
                Description = game.Description,
                ReleasedOn = game.ReleasedOn.GetValueOrDefault(),
                Singleplayer = game.Singleplayer,
                Icon = game.Icon
            };

            if (game.Genres != null && game.Genres.Count > 0)
                manifest.Genre = game.Genres.Select(g => g.Name).ToArray();

            if (game.Tags != null && game.Tags.Count > 0)
                manifest.Tags = game.Tags.Select(g => g.Name).ToArray();

            if (game.Publishers != null && game.Publishers.Count > 0)
                manifest.Publishers = game.Publishers.Select(g => g.Name).ToArray();

            if (game.Developers != null && game.Developers.Count > 0)
                manifest.Developers = game.Developers.Select(g => g.Name).ToArray();

            if (game.Archives != null && game.Archives.Count > 0)
                manifest.Version = game.Archives.OrderByDescending(a => a.CreatedOn).First().Version;

            if (game.Actions != null && game.Actions.Count > 0)
            {
                manifest.Actions = game.Actions.Select(a => new GameAction()
                {
                    Name = a.Name,
                    Arguments = a.Arguments,
                    Path = a.Path,
                    WorkingDirectory = a.WorkingDirectory,
                    IsPrimaryAction = a.PrimaryAction,
                    SortOrder = a.SortOrder,
                }).ToArray();
            }

            if (game.MultiplayerModes != null && game.MultiplayerModes.Count > 0)
            {
                var local = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.Local);
                var lan = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.Lan);
                var online = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.Online);

                if (local != null)
                    manifest.LocalMultiplayer = new MultiplayerInfo()
                    {
                        MinPlayers = local.MinPlayers,
                        MaxPlayers = local.MaxPlayers,
                    };

                if (lan != null)
                    manifest.LanMultiplayer = new MultiplayerInfo()
                    {
                        MinPlayers = lan.MinPlayers,
                        MaxPlayers = lan.MaxPlayers,
                    };

                if (online != null)
                    manifest.LocalMultiplayer = new MultiplayerInfo()
                    {
                        MaxPlayers = online.MinPlayers,
                        MinPlayers = online.MaxPlayers,
                    };
            }

            if (game.SavePaths != null && game.SavePaths.Count > 0)
            {
                manifest.SavePaths = game.SavePaths.Select(p => new SDK.SavePath()
                {
                    Id = p.Id,
                    Path = p.Path,
                    Type = p.Type.ToString()
                });
            }

            return manifest;
        }

        public byte[] GetIcon(Game game)
        {
            var cachedPath = $"Icon/{game.Id}.png";

            if (File.Exists(cachedPath))
                return File.ReadAllBytes(cachedPath);
            else
            {
                #if WINDOWS
                try
                {
                    if (game.Archives == null || game.Archives.Count == 0)
                        throw new FileNotFoundException();

                    var archive = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();

                    Bitmap bitmap = null;

                    var iconReference = ArchiveService.ReadFile(archive.ObjectKey, game.Icon);

                    if (IsWinPEFile(iconReference))
                    {
                        var tmp = System.IO.Path.GetTempFileName();

                        System.IO.File.WriteAllBytes(tmp, iconReference);

                        var icon = System.Drawing.Icon.ExtractAssociatedIcon(tmp);

                        bitmap = icon.ToBitmap();
                    }
                    else
                    {
                        using (var ms = new MemoryStream(iconReference))
                        {
                            bitmap = (Bitmap)Bitmap.FromStream(ms);
                        }
                    }

                    var iconPng = ConvertToPng(bitmap);

                    File.WriteAllBytes(cachedPath, iconPng);

                    return iconPng;
                }
                catch (Exception ex)
                {

                }
                #endif

                return File.ReadAllBytes("favicon.png");
            }
        }

        private static bool IsWinPEFile(byte[] file)
        {
            var mz = new byte[2];

            using (var ms = new MemoryStream(file))
            {
                ms.Read(mz, 0, 2);
            }

            return System.Text.Encoding.UTF8.GetString(mz) == "MZ";
        }

        private static byte[] ConvertToPng(Image img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);

                return stream.ToArray();
            }
        }
    }
}
