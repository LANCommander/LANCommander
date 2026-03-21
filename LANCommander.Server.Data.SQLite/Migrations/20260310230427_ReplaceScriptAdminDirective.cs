using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceScriptAdminDirective : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Scripts SET Contents = replace (Contents, '# Requires Admin', '#Requires -RunAsAdministrator') where Contents like '%# Requires Admin%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Scripts SET Contents = replace (Contents, '#Requires -RunAsAdministrator', '# Requires Admin') where Contents like '#Requires -RunAsAdministrator';");
        }
    }
}
