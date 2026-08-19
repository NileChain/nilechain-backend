using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HandoverHygiene : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KybKind",
                table: "FarmDocument",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<Guid>(
                name: "GrantedByAdminUserId",
                table: "FarmCertification",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaDueAt",
                table: "Dispute",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispute_SlaDueAt",
                table: "Dispute",
                column: "SlaDueAt");

            migrationBuilder.Sql(
                """
                UPDATE [Dispute]
                SET [SlaDueAt] = DATEADD(hour, 48, [CreatedAt])
                WHERE [SlaDueAt] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dispute_SlaDueAt",
                table: "Dispute");

            migrationBuilder.DropColumn(
                name: "KybKind",
                table: "FarmDocument");

            migrationBuilder.DropColumn(
                name: "GrantedByAdminUserId",
                table: "FarmCertification");

            migrationBuilder.DropColumn(
                name: "SlaDueAt",
                table: "Dispute");
        }
    }
}
