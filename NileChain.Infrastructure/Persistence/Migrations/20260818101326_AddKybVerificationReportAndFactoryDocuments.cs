using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKybVerificationReportAndFactoryDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FactoryDocument",
                columns: table => new
                {
                    FactoryDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KybKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "Other")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryDocument", x => x.FactoryDocumentId);
                    table.ForeignKey(
                        name: "FK_FactoryDocument_Factory_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "Factory",
                        principalColumn: "FactoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KybVerificationReport",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrustScore = table.Column<int>(type: "int", nullable: false),
                    OverallSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BreakdownJson = table.Column<string>(type: "nvarchar(max)", maxLength: 100000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KybVerificationReport", x => x.ReportId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryDocument_FactoryId",
                table: "FactoryDocument",
                column: "FactoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactoryDocument");

            migrationBuilder.DropTable(
                name: "KybVerificationReport");
        }
    }
}
