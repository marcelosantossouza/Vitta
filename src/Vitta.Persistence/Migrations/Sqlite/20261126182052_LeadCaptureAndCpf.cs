using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class LeadCaptureAndCpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConsentAt",
                table: "prospects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentText",
                table: "prospects",
                type: "TEXT",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "prospects",
                type: "TEXT",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lead_capture_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    BrokerName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Headline = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    WhatsApp = table.Column<string>(type: "TEXT", maxLength: 13, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_capture_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prospects_Cpf",
                table: "prospects",
                column: "Cpf");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lead_capture_settings");

            migrationBuilder.DropIndex(
                name: "IX_prospects_Cpf",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "ConsentAt",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "ConsentText",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "prospects");
        }
    }
}
