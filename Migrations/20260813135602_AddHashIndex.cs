using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileTrackingAndProcessingServices.Migrations
{
    /// <inheritdoc />
    public partial class AddHashIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_Hash",
                table: "TrackedFiles",
                column: "Hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackedFiles_Hash",
                table: "TrackedFiles");
        }
    }
}
