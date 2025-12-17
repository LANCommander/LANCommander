using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGameIconProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate any old icons from the filesystem
            if (Directory.Exists("Icon"))
                foreach (var file in Directory.EnumerateFiles("Icon"))
                {
                    var objectKey = Path.GetFileNameWithoutExtension(file);

                    var mediaId = Guid.NewGuid();
                    var fileId = Guid.NewGuid();

                    // Probably not an issue, but we'll check to make sure these are valid GUIDs just in case
                    if (Guid.TryParse(objectKey, out var gameId))
                    {
                        var sql = $@"
                            INSERT INTO Media
                            (Id, FileId, Type, SourceUrl, GameId, MimeType, CreatedOn, UpdatedOn)
                            SELECT
                            '{mediaId.ToString().ToUpper()}', '{fileId.ToString().ToUpper()}', '{(int)MediaType.Icon}', '', '{gameId.ToString().ToUpper()}', 'image/png', DateTime('now'), DateTime('now')
                            WHERE EXISTS (SELECT 1 FROM Games WHERE Id = '{gameId.ToString().ToUpper()}')
                        ";

                        migrationBuilder.Sql(sql);
                    }

                    File.Move(file, AppPaths.GetConfigPath("Media", fileId.ToString()));
                }

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Games",
                type: "TEXT",
                nullable: true);
        }
    }
}
