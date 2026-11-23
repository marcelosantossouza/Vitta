using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class PortfolioRowDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                schema: "vitta",
                table: "portfolio_rows",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PolicyNumber",
                schema: "vitta",
                table: "portfolio_rows",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "vitta",
                table: "portfolio_rows",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                schema: "vitta",
                table: "portfolio_rows",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                schema: "vitta",
                table: "portfolio_rows",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Competence",
                schema: "vitta",
                table: "portfolio_rows",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Coverages",
                schema: "vitta",
                table: "portfolio_rows",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Extras",
                schema: "vitta",
                table: "portfolio_rows",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Installment",
                schema: "vitta",
                table: "portfolio_rows",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                schema: "vitta",
                table: "portfolio_rows",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                schema: "vitta",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                schema: "vitta",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                schema: "vitta",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "Competence",
                schema: "vitta",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "Coverages",
                schema: "vitta",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "Extras",
                schema: "vitta",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "Installment",
                schema: "vitta",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "vitta",
                table: "portfolio_rows");

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                schema: "vitta",
                table: "portfolio_rows",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PolicyNumber",
                schema: "vitta",
                table: "portfolio_rows",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
