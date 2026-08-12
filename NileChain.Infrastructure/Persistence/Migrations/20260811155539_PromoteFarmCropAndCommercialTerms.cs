using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromoteFarmCropAndCommercialTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FarmCrop",
                columns: table => new
                {
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvailableQuantityTons = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AvailableFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvailableTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MinPricePerTon = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmCrop", x => new { x.FarmId, x.CropTypeId });
                    table.CheckConstraint("CK_FarmCrop_AvailabilityDates", "[AvailableTo] IS NULL OR [AvailableFrom] IS NULL OR [AvailableTo] >= [AvailableFrom]");
                    table.CheckConstraint("CK_FarmCrop_AvailableQuantityNonNegative", "[AvailableQuantityTons] IS NULL OR [AvailableQuantityTons] >= 0");
                    table.CheckConstraint("CK_FarmCrop_MinPriceNonNegative", "[MinPricePerTon] IS NULL OR [MinPricePerTon] >= 0");
                    table.ForeignKey(
                        name: "FK_FarmCrop_CropType_CropTypeId",
                        column: x => x.CropTypeId,
                        principalTable: "CropType",
                        principalColumn: "CropTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmCrop_Farm_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farm",
                        principalColumn: "FarmId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO [FarmCrop] ([FarmId], [CropTypeId])
                SELECT [FarmsFarmId], [CropTypesCropTypeId]
                FROM [FarmCropType];
                """);

            migrationBuilder.DropTable(
                name: "FarmCropType");

            migrationBuilder.CreateIndex(
                name: "IX_FarmCrop_CropTypeId",
                table: "FarmCrop",
                column: "CropTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FarmCropType",
                columns: table => new
                {
                    CropTypesCropTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmsFarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmCropType", x => new { x.CropTypesCropTypeId, x.FarmsFarmId });
                    table.ForeignKey(
                        name: "FK_FarmCropType_CropType_CropTypesCropTypeId",
                        column: x => x.CropTypesCropTypeId,
                        principalTable: "CropType",
                        principalColumn: "CropTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmCropType_Farm_FarmsFarmId",
                        column: x => x.FarmsFarmId,
                        principalTable: "Farm",
                        principalColumn: "FarmId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO [FarmCropType] ([FarmsFarmId], [CropTypesCropTypeId])
                SELECT [FarmId], [CropTypeId]
                FROM [FarmCrop];
                """);

            migrationBuilder.DropTable(
                name: "FarmCrop");

            migrationBuilder.CreateIndex(
                name: "IX_FarmCropType_FarmsFarmId",
                table: "FarmCropType",
                column: "FarmsFarmId");
        }
    }
}
