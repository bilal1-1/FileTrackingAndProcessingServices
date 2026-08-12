using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileTrackingAndProcessingServices.Migrations
{
    /// <inheritdoc />
    public partial class AddHashToTrackedFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hash",
                table: "TrackedFiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hash",
                table: "TrackedFiles");
        }
    }
}
