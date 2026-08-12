using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260812020000_AddContractIntegrityChain")]
    public partial class AddContractIntegrityChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractIntegrityAnchor",
                columns: table => new
                {
                    AnchorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChainIndex = table.Column<long>(type: "bigint", nullable: false),
                    TxRef = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnchoredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractIntegrityAnchor", x => x.AnchorId);
                    table.ForeignKey(
                        name: "FK_ContractIntegrityAnchor_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractIntegrityAnchor_ChainIndex",
                table: "ContractIntegrityAnchor",
                column: "ChainIndex",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractIntegrityAnchor_ContentHash",
                table: "ContractIntegrityAnchor",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractIntegrityAnchor_ContractId_Status",
                table: "ContractIntegrityAnchor",
                columns: new[] { "ContractId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractIntegrityAnchor");
        }
    }
}
