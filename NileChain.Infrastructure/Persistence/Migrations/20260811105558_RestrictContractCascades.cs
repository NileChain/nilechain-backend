using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestrictContractCascades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contract_FarmMatch_MatchId",
                table: "Contract");

            migrationBuilder.DropForeignKey(
                name: "FK_FarmMatch_SupplyRequest_RequestId",
                table: "FarmMatch");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplyRequest_Factory_FactoryId",
                table: "SupplyRequest");

            migrationBuilder.DropIndex(
                name: "IX_Review_ContractId",
                table: "Review");

            migrationBuilder.DropIndex(
                name: "IX_FarmMatch_RequestId",
                table: "FarmMatch");

            migrationBuilder.AddColumn<string>(
                name: "MatchedGovernorate",
                table: "FarmMatch",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "AspNetUsers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Review_ContractId_ReviewerId",
                table: "Review",
                columns: new[] { "ContractId", "ReviewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FarmMatch_RequestId_FarmId",
                table: "FarmMatch",
                columns: new[] { "RequestId", "FarmId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL AND [PhoneNumber] != ''");

            migrationBuilder.AddForeignKey(
                name: "FK_Contract_FarmMatch_MatchId",
                table: "Contract",
                column: "MatchId",
                principalTable: "FarmMatch",
                principalColumn: "MatchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FarmMatch_SupplyRequest_RequestId",
                table: "FarmMatch",
                column: "RequestId",
                principalTable: "SupplyRequest",
                principalColumn: "RequestId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplyRequest_Factory_FactoryId",
                table: "SupplyRequest",
                column: "FactoryId",
                principalTable: "Factory",
                principalColumn: "FactoryId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contract_FarmMatch_MatchId",
                table: "Contract");

            migrationBuilder.DropForeignKey(
                name: "FK_FarmMatch_SupplyRequest_RequestId",
                table: "FarmMatch");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplyRequest_Factory_FactoryId",
                table: "SupplyRequest");

            migrationBuilder.DropIndex(
                name: "IX_Review_ContractId_ReviewerId",
                table: "Review");

            migrationBuilder.DropIndex(
                name: "IX_FarmMatch_RequestId_FarmId",
                table: "FarmMatch");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MatchedGovernorate",
                table: "FarmMatch");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Review_ContractId",
                table: "Review",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmMatch_RequestId",
                table: "FarmMatch",
                column: "RequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contract_FarmMatch_MatchId",
                table: "Contract",
                column: "MatchId",
                principalTable: "FarmMatch",
                principalColumn: "MatchId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FarmMatch_SupplyRequest_RequestId",
                table: "FarmMatch",
                column: "RequestId",
                principalTable: "SupplyRequest",
                principalColumn: "RequestId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplyRequest_Factory_FactoryId",
                table: "SupplyRequest",
                column: "FactoryId",
                principalTable: "Factory",
                principalColumn: "FactoryId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
