using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MvpHonestySprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EligibilitySnapshotJson",
                table: "FarmMatch",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExcludedByFactory",
                table: "FarmMatch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ContractAttachment",
                columns: table => new
                {
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAttachment", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_ContractAttachment_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarmMatch_RequestId_Excluded",
                table: "FarmMatch",
                columns: new[] { "RequestId", "IsExcludedByFactory" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAttachment_ContractId",
                table: "ContractAttachment",
                column: "ContractId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractAttachment");

            migrationBuilder.DropIndex(
                name: "IX_FarmMatch_RequestId_Excluded",
                table: "FarmMatch");

            migrationBuilder.DropColumn(
                name: "EligibilitySnapshotJson",
                table: "FarmMatch");

            migrationBuilder.DropColumn(
                name: "IsExcludedByFactory",
                table: "FarmMatch");
        }
    }
}
