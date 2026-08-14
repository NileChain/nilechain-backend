using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260813050000_AddContractTermDates")]
    public partial class AddContractTermDates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartsAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndsAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingStartsAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingEndsAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DateAmendmentProposedByUserId",
                table: "Contract",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAmendmentProposedAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StartsAt", table: "Contract");
            migrationBuilder.DropColumn(name: "EndsAt", table: "Contract");
            migrationBuilder.DropColumn(name: "PendingStartsAt", table: "Contract");
            migrationBuilder.DropColumn(name: "PendingEndsAt", table: "Contract");
            migrationBuilder.DropColumn(name: "DateAmendmentProposedByUserId", table: "Contract");
            migrationBuilder.DropColumn(name: "DateAmendmentProposedAt", table: "Contract");
        }
    }
}
