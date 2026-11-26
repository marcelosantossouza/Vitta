using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ProspectProfileAndImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "prospects",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "prospects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "prospects",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Discounts",
                table: "prospects",
                type: "REAL",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedSalary",
                table: "prospects",
                type: "REAL",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportId",
                table: "prospects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Loans",
                table: "prospects",
                type: "REAL",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "prospects",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone2",
                table: "prospects",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "prospects",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "prospects",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prospect_imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TemplateName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<int>(type: "INTEGER", nullable: false),
                    Updated = table.Column<int>(type: "INTEGER", nullable: false),
                    Skipped = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospect_imports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prospects_City",
                table: "prospects",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_prospects_ImportId",
                table: "prospects",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_prospects_State",
                table: "prospects",
                column: "State");

            migrationBuilder.AddForeignKey(
                name: "FK_prospects_prospect_imports_ImportId",
                table: "prospects",
                column: "ImportId",
                principalTable: "prospect_imports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prospects_prospect_imports_ImportId",
                table: "prospects");

            migrationBuilder.DropTable(
                name: "prospect_imports");

            migrationBuilder.DropIndex(
                name: "IX_prospects_City",
                table: "prospects");

            migrationBuilder.DropIndex(
                name: "IX_prospects_ImportId",
                table: "prospects");

            migrationBuilder.DropIndex(
                name: "IX_prospects_State",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "City",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Discounts",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "EstimatedSalary",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "ImportId",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Loans",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Phone2",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "State",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "prospects");
        }
    }
}
