using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ProspectCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Activity",
                table: "prospects",
                type: "TEXT",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                table: "prospects",
                type: "TEXT",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanySize",
                table: "prospects",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "prospects",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OpenedOn",
                table: "prospects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_prospects_Cnpj",
                table: "prospects",
                column: "Cnpj");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prospects_Cnpj",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Activity",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "CompanySize",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "OpenedOn",
                table: "prospects");
        }
    }
}
