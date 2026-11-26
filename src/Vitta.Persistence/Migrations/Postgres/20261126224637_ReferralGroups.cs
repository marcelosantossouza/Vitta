using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ReferralGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferralGroupId",
                schema: "vitta",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "referral_groups",
                schema: "vitta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SharePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ShareLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clients_ReferralGroupId",
                schema: "vitta",
                table: "clients",
                column: "ReferralGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_referral_groups_Code",
                schema: "vitta",
                table: "referral_groups",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral_groups",
                schema: "vitta");

            migrationBuilder.DropIndex(
                name: "IX_clients_ReferralGroupId",
                schema: "vitta",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "ReferralGroupId",
                schema: "vitta",
                table: "clients");
        }
    }
}
