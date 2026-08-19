using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260818180000_AddDealFlexibility")]
    public partial class AddDealFlexibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FactoryApprovedOneRingExpansion",
                table: "SupplyRequest",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShortlistTakeLimit",
                table: "SupplyRequest",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGeographicExpansion",
                table: "FarmMatch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MatchNegotiationRound",
                columns: table => new
                {
                    RoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QuantityTons = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PricePerTon = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchNegotiationRound", x => x.RoundId);
                    table.ForeignKey(
                        name: "FK_MatchNegotiationRound_FarmMatch_MatchId",
                        column: x => x.MatchId,
                        principalTable: "FarmMatch",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchNegotiationRound_MatchId_CreatedAt",
                table: "MatchNegotiationRound",
                columns: new[] { "MatchId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MatchNegotiationRound");

            migrationBuilder.DropColumn(name: "IsGeographicExpansion", table: "FarmMatch");
            migrationBuilder.DropColumn(name: "FactoryApprovedOneRingExpansion", table: "SupplyRequest");
            migrationBuilder.DropColumn(name: "ShortlistTakeLimit", table: "SupplyRequest");
        }
    }
}
