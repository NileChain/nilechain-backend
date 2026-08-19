using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedElectronicSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractAuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StateHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAuditLog_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAuditLog_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Append-only. Application must not UPDATE or DELETE rows.");

            migrationBuilder.CreateTable(
                name: "ContractSignatureRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContractHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignatureToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConsentText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSignatureRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractSignatureRecord_AspNetUsers_SignerId",
                        column: x => x.SignerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractSignatureRecord_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SigningOtp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OtpHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningOtp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SigningOtp_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SigningOtp_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAuditLog_ActorId",
                table: "ContractAuditLog",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAuditLog_ContractId_Timestamp",
                table: "ContractAuditLog",
                columns: new[] { "ContractId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureRecord_ContractId_SignerId",
                table: "ContractSignatureRecord",
                columns: new[] { "ContractId", "SignerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatureRecord_SignerId",
                table: "ContractSignatureRecord",
                column: "SignerId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningOtp_ContractId",
                table: "SigningOtp",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningOtp_UserId_ContractId_CreatedAt",
                table: "SigningOtp",
                columns: new[] { "UserId", "ContractId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractAuditLog");

            migrationBuilder.DropTable(
                name: "ContractSignatureRecord");

            migrationBuilder.DropTable(
                name: "SigningOtp");
        }
    }
}
