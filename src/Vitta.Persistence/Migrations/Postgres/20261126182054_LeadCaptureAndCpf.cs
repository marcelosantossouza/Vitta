using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class LeadCaptureAndCpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsentAt",
                schema: "vitta",
                table: "prospects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentText",
                schema: "vitta",
                table: "prospects",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                schema: "vitta",
                table: "prospects",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lead_capture_settings",
                schema: "vitta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    BrokerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Headline = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WhatsApp = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_capture_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prospects_Cpf",
                schema: "vitta",
                table: "prospects",
                column: "Cpf");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lead_capture_settings",
                schema: "vitta");

            migrationBuilder.DropIndex(
                name: "IX_prospects_Cpf",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "ConsentAt",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "ConsentText",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Cpf",
                schema: "vitta",
                table: "prospects");
        }
    }
}
