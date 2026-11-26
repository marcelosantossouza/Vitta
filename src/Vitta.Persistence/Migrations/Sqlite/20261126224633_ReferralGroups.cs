using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ReferralGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferralGroupId",
                table: "clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "referral_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SharePercent = table.Column<double>(type: "REAL", precision: 5, scale: 2, nullable: true),
                    ShareLabel = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clients_ReferralGroupId",
                table: "clients",
                column: "ReferralGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_referral_groups_Code",
                table: "referral_groups",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral_groups");

            migrationBuilder.DropIndex(
                name: "IX_clients_ReferralGroupId",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "ReferralGroupId",
                table: "clients");
        }
    }
}
