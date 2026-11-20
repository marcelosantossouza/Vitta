using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class UsersAndPortfolioSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFirstImport",
                schema: "vitta",
                table: "portfolio_imports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "portfolio_rows",
                schema: "vitta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HolderCpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    ProductName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    MonthlyPremium = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PortfolioImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio_rows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_portfolio_rows_portfolio_imports_PortfolioImportId",
                        column: x => x.PortfolioImportId,
                        principalSchema: "vitta",
                        principalTable: "portfolio_imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "vitta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_changes_Type",
                schema: "vitta",
                table: "portfolio_changes",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_rows_PortfolioImportId",
                schema: "vitta",
                table: "portfolio_rows",
                column: "PortfolioImportId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "vitta",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_rows",
                schema: "vitta");

            migrationBuilder.DropTable(
                name: "users",
                schema: "vitta");

            migrationBuilder.DropIndex(
                name: "IX_portfolio_changes_Type",
                schema: "vitta",
                table: "portfolio_changes");

            migrationBuilder.DropColumn(
                name: "IsFirstImport",
                schema: "vitta",
                table: "portfolio_imports");
        }
    }
}
