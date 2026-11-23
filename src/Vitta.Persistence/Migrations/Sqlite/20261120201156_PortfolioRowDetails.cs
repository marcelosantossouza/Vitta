using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PortfolioRowDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PolicyNumber",
                table: "portfolio_rows",
                type: "TEXT",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "portfolio_rows",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CommissionAmount",
                table: "portfolio_rows",
                type: "REAL",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CommissionRate",
                table: "portfolio_rows",
                type: "REAL",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Competence",
                table: "portfolio_rows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Coverages",
                table: "portfolio_rows",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Extras",
                table: "portfolio_rows",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Installment",
                table: "portfolio_rows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "portfolio_rows",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "Competence",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "Coverages",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "Extras",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "Installment",
                table: "portfolio_rows");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "portfolio_rows");

            migrationBuilder.AlterColumn<string>(
                name: "PolicyNumber",
                table: "portfolio_rows",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
