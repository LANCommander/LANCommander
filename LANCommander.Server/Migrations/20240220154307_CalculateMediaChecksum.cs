using Force.Crc32;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class CalculateMediaChecksum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var settings = SettingService.GetSettings();

            var files = Directory.EnumerateFiles(settings.Media.StoragePath);

            foreach (var file in files)
            {
                try
                {
                    uint crc = 0;

                    using (FileStream fs = File.Open(file, FileMode.Open))
                    {
                        var buffer = new byte[4096];

                        while (true)
                        {
                            var count = fs.Read(buffer, 0, buffer.Length);

                            if (count == 0)
                                break;

                            crc = Crc32Algorithm.Append(crc, buffer, 0, count);
                        }
                    }

                    migrationBuilder.Sql($"UPDATE Media SET Crc32 = '{crc.ToString("X")}' WHERE FileId = '{file.Replace(settings.Media.StoragePath + Path.DirectorySeparatorChar, "").ToUpper()}'");
                }
                catch (Exception ex)
                {

                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
