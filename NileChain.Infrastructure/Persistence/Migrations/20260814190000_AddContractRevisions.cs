using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260814190000_AddContractRevisions")]
    public partial class AddContractRevisions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractRevision",
                columns: table => new
                {
                    ContractRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RevisedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisedByParty = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRevision", x => x.ContractRevisionId);
                    table.ForeignKey(
                        name: "FK_ContractRevision_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRevision_ContractId_CreatedAt",
                table: "ContractRevision",
                columns: new[] { "ContractId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ContractRevision");
        }
    }
}
