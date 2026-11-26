using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ProspectProfileAndImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "vitta",
                table: "prospects",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                schema: "vitta",
                table: "prospects",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "vitta",
                table: "prospects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discounts",
                schema: "vitta",
                table: "prospects",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedSalary",
                schema: "vitta",
                table: "prospects",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportId",
                schema: "vitta",
                table: "prospects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Loans",
                schema: "vitta",
                table: "prospects",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                schema: "vitta",
                table: "prospects",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone2",
                schema: "vitta",
                table: "prospects",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "vitta",
                table: "prospects",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                schema: "vitta",
                table: "prospects",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prospect_imports",
                schema: "vitta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospect_imports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prospects_City",
                schema: "vitta",
                table: "prospects",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_prospects_ImportId",
                schema: "vitta",
                table: "prospects",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_prospects_State",
                schema: "vitta",
                table: "prospects",
                column: "State");

            migrationBuilder.AddForeignKey(
                name: "FK_prospects_prospect_imports_ImportId",
                schema: "vitta",
                table: "prospects",
                column: "ImportId",
                principalSchema: "vitta",
                principalTable: "prospect_imports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prospects_prospect_imports_ImportId",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropTable(
                name: "prospect_imports",
                schema: "vitta");

            migrationBuilder.DropIndex(
                name: "IX_prospects_City",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropIndex(
                name: "IX_prospects_ImportId",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropIndex(
                name: "IX_prospects_State",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Discounts",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "EstimatedSalary",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "ImportId",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Loans",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Occupation",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "Phone2",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "vitta",
                table: "prospects");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                schema: "vitta",
                table: "prospects");
        }
    }
}
