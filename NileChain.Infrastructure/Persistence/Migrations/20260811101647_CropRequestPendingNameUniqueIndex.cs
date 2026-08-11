using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CropRequestPendingNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CropRequest_Name_Status",
                table: "CropRequest");

            migrationBuilder.CreateIndex(
                name: "IX_CropRequest_Name_Pending",
                table: "CropRequest",
                columns: new[] { "Name", "Status" },
                unique: true,
                filter: "[Status] = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CropRequest_Name_Pending",
                table: "CropRequest");

            migrationBuilder.CreateIndex(
                name: "IX_CropRequest_Name_Status",
                table: "CropRequest",
                columns: new[] { "Name", "Status" });
        }
    }
}
