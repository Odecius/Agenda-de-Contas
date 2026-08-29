using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendadorContas.data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "family_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_invitations", x => x.Id);
                    table.CheckConstraint("ck_family_invitations_acceptor", "(\"AcceptedAtUtc\" IS NULL) = (\"AcceptedByUserId\" IS NULL)");
                    table.CheckConstraint("ck_family_invitations_expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("ck_family_invitations_resolution", "NOT (\"AcceptedAtUtc\" IS NOT NULL AND \"RevokedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("ck_family_invitations_role", "\"Role\" IN ('Admin', 'Member')");
                    table.ForeignKey(
                        name: "FK_family_invitations_app_users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_family_invitations_app_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_family_invitations_families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_family_invitations_AcceptedByUserId",
                table: "family_invitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_family_invitations_CreatedByUserId",
                table: "family_invitations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_family_invitations_FamilyId_NormalizedEmail_CreatedAtUtc",
                table: "family_invitations",
                columns: new[] { "FamilyId", "NormalizedEmail", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_family_invitations_TokenHash",
                table: "family_invitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "family_invitations");
        }
    }
}
