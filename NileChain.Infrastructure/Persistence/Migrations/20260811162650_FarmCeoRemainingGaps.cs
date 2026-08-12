using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FarmCeoRemainingGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CounterAccepted",
                table: "FarmMatch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CounterDeliveryDate",
                table: "FarmMatch",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterNote",
                table: "FarmMatch",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CounterPricePerTon",
                table: "FarmMatch",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CounterQuantityTons",
                table: "FarmMatch",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CounteredAt",
                table: "FarmMatch",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "FarmCrop",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Farm",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FarmImage",
                columns: table => new
                {
                    FarmImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmImage", x => x.FarmImageId);
                    table.ForeignKey(
                        name: "FK_FarmImage_Farm_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farm",
                        principalColumn: "FarmId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarmImage_FarmId_SortOrder",
                table: "FarmImage",
                columns: new[] { "FarmId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarmImage");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CounterAccepted",
                table: "FarmMatch");

            migrationBuilder.DropColumn(
                name: "CounterDeliveryDate",
                table: "FarmMatch");

            migrationBuilder.DropColumn(
                name: "CounterNote",
                table: "FarmMatch");

            migrationBuilder.DropColumn(
                name: "CounterPricePerTon",
                table: "FarmMatch");

            migrationBuilder.DropColumn(
                name: "CounterQuantityTons",
                table: "FarmMatch");

            migrationBuilder.DropColumn(
                name: "CounteredAt",
                table: "FarmMatch");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "FarmCrop");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Farm");
        }
    }
}
