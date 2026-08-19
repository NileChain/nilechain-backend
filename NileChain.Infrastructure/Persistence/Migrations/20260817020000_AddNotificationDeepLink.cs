using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260817020000_AddNotificationDeepLink")]
    public partial class AddNotificationDeepLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notification",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                table: "Notification",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "Notification",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RelatedEntityId",
                table: "Notification",
                column: "RelatedEntityId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notification_RelatedEntityId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "Notification");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notification",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
