using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dispute",
                columns: table => new
                {
                    DisputeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RaisedByParty = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RaisedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OutcomeFavor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnderReviewAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispute", x => x.DisputeId);
                    table.ForeignKey(
                        name: "FK_Dispute_AspNetUsers_RaisedByUserId",
                        column: x => x.RaisedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dispute_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dispute_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dispute_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisputeEvent",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisputeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeEvent", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_DisputeEvent_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisputeEvent_Dispute_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Dispute",
                        principalColumn: "DisputeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DisputeEvidence",
                columns: table => new
                {
                    DisputeEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisputeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeEvidence", x => x.DisputeEvidenceId);
                    table.ForeignKey(
                        name: "FK_DisputeEvidence_Dispute_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Dispute",
                        principalColumn: "DisputeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dispute_ContractId_Active",
                table: "Dispute",
                column: "ContractId",
                unique: true,
                filter: "Status IN ('Open', 'UnderReview')");

            migrationBuilder.CreateIndex(
                name: "IX_Dispute_RaisedByUserId",
                table: "Dispute",
                column: "RaisedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispute_ResolvedByUserId",
                table: "Dispute",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispute_ReviewedByUserId",
                table: "Dispute",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispute_Status_Type",
                table: "Dispute",
                columns: new[] { "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvent_ActorUserId",
                table: "DisputeEvent",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvent_DisputeId",
                table: "DisputeEvent",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_DisputeId",
                table: "DisputeEvidence",
                column: "DisputeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisputeEvent");

            migrationBuilder.DropTable(
                name: "DisputeEvidence");

            migrationBuilder.DropTable(
                name: "Dispute");
        }
    }
}
