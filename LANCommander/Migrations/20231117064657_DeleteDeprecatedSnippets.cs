using LANCommander.Data.Enums;
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

            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$NewName = $args[0]' || char(13) || char(10), '')");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$OldName = \"\"' || char(13) || char(10) || 'if ($args[1]) {' || char(13) || char(10) || char(9) || '$OldName = $args[1]' || char(13) || char(10) || '}' || char(13) || char(10), '')");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$InstallDir = $PSScriptRoot' || char(13) || char(10), '')");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$InstallDir', '$InstallDirectory')");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$NewName', '$NewPlayerAlias')");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$OldName', '$OldPlayerAlias')");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, 'function Write-ReplaceContentInFile', 'function Write-ReplaceContentInFile-Old')");
            migrationBuilder.Sql($"UPDATE Scripts SET Contents = REPLACE(Contents, '$args[0]', '$AllocatedKey') WHERE Type = {(int)ScriptType.Install}");
            migrationBuilder.Sql($"UPDATE Scripts SET Contents = REPLACE(Contents, '$args[0]', '$AllocatedKey') WHERE Type = {(int)ScriptType.KeyChange}");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, 'Add-Type -AssemblyName System.Windows.Forms' || char(13) || char(10), '')");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$Display = [System.Windows.Forms.Screen]::AllScreens | Where-Object Primary | Select Bounds', '$Display = Get-PrimaryDisplay')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
