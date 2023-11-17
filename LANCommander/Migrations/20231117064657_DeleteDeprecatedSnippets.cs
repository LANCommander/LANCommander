using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class DeleteDeprecatedSnippets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            string[] snippetsToRemove = new string[]
            {
                "Examples\\Replace Content In File.ps1",
                "Examples\\Separate ASCII Bytes.ps1",
                "Examples\\String to ASCII Bytes.ps1",
                "Functions\\Get-43Resolution.ps1",
                "Functions\\Get-AsciiBytes.ps1",
                "Functions\\Patch-Binary.ps1",
                "Functions\\Separate-AsciiBytes.ps1",
                "Variables\\Display.ps1",
                "Variables\\InstallDir.ps1",
                "Variables\\NewName.ps1",
                "Variables\\OldName.ps1",
            };

            foreach (var snippet in snippetsToRemove)
            {
                var path = Path.Combine("Snippets", snippet);

                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
