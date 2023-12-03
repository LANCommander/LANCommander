using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class FixScriptDisplayBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$Display.Width', '$Display.Bounds.Width')");
            migrationBuilder.Sql("UPDATE Scripts SET Contents = REPLACE(Contents, '$Display.Height', '$Display.Bounds.Height')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
