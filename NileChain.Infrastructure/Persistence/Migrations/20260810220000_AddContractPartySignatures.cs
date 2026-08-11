using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations;

[DbContext(typeof(NileChainDbContext))]
[Migration("20260810220000_AddContractPartySignatures")]
public partial class AddContractPartySignatures : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Contract",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(20)",
            oldMaxLength: 20);

        migrationBuilder.AddColumn<DateTime>(
            name: "FactorySignedAt",
            table: "Contract",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "FarmSignedAt",
            table: "Contract",
            type: "datetime2",
            nullable: true);

        // Preserve historically fully-signed contracts: both parties signed.
        migrationBuilder.Sql("""
            UPDATE [Contract]
            SET [FactorySignedAt] = [SignedAt],
                [FarmSignedAt] = [SignedAt]
            WHERE [Status] = N'Signed'
              AND [SignedAt] IS NOT NULL
              AND [FactorySignedAt] IS NULL
              AND [FarmSignedAt] IS NULL;
            """);

        // Mis-marked "Signed" from factory-only approve (no farm action) cannot be recovered;
        // leave Signed rows that already have SignedAt as bilateral for data safety.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FactorySignedAt",
            table: "Contract");

        migrationBuilder.DropColumn(
            name: "FarmSignedAt",
            table: "Contract");

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Contract",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(40)",
            oldMaxLength: 40);
    }
}
