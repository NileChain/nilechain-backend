using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260817040000_W0P1ChannelOutbox")]
    public partial class W0P1ChannelOutbox : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelMessages",
                columns: table => new
                {
                    ChannelMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMessages", x => x.ChannelMessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_CreatedAt",
                table: "ChannelMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_Status",
                table: "ChannelMessages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_TemplateKey",
                table: "ChannelMessages",
                column: "TemplateKey");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelMessages");
        }
    }
}
