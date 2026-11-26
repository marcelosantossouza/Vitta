using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ProspectCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Activity",
                schema: "vitta",
                table: "prospects",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                schema: "vitta",
                table: "prospects",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanySize",
                schema: "vitta",
                table: "prospects",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                schema: "vitta",
                table: "prospects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OpenedOn",
                schema: "vitta",
                table: "prospects",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_prospects_Cnpj",
                schema: "vitta",
                table: "prospects",
                column: "Cnpj");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prospects_Cnpj",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Activity",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "CompanySize",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "LegalName",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "OpenedOn",
                schema: "vitta",
                table: "prospects");
        }
    }
}
