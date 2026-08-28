using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileTrackingAndProcessingServices.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueFilePathIndex : Migration
    {
        /// <summary>
        /// DİKKAT: Tabloda aynı FilePath'e sahip birden fazla satır varsa bu
        /// migration hata verir ve uygulanmaz — benzersiz index oluşturulamaz.
        /// Bu bilinçli: yinelenen kayıtları sessizce silmek veri kaybı olurdu,
        /// hangisinin doğru olduğuna kod karar veremez.
        ///
        /// Böyle bir durumda önce kopyalar elle temizlenmeli. Her yoldan en
        /// küçük Id'li kaydı tutan sorgu:
        ///
        ///   DELETE FROM "TrackedFiles" a
        ///   USING "TrackedFiles" b
        ///   WHERE a."FilePath" = b."FilePath" AND a."Id" &gt; b."Id";
        ///
        /// Demo veritabanını sıfırlamak da yeterli: docker compose down -v
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_FilePath",
                table: "TrackedFiles",
                column: "FilePath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackedFiles_FilePath",
                table: "TrackedFiles");
        }
    }
}
