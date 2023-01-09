using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Helpers;
using System.Drawing;

namespace LANCommander.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        public GameService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }

        public override async Task Delete(Game game)
        {
            foreach (var archive in game.Archives.OrderByDescending(a => a.CreatedOn))
            {
                FileHelpers.DeleteIfExists($"Icon/{game.Id}.png".ToPath());
                FileHelpers.DeleteIfExists($"Upload/{archive.ObjectKey}".ToPath());
            }

            await base.Delete(game);
        }

        public byte[] GetIcon(Game game)
        {
            var cachedPath = $"Icon/{game.Id}.png";

            if (File.Exists(cachedPath))
                return File.ReadAllBytes(cachedPath);
            else
            {
                if (game.Archives == null || game.Archives.Count == 0)
                    throw new FileNotFoundException();

                var archive = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();

                Bitmap bitmap = null;

                var manifest = ArchiveService.ReadManifest(archive.ObjectKey);
                var iconReference = ArchiveService.ReadFile(archive.ObjectKey, manifest.Icon);

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
