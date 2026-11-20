using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class UsersAndPortfolioSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFirstImport",
                table: "portfolio_imports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "portfolio_rows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PolicyNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    HolderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HolderCpf = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    MonthlyPremium = table.Column<double>(type: "REAL", precision: 12, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    PortfolioImportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio_rows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_portfolio_rows_portfolio_imports_PortfolioImportId",
                        column: x => x.PortfolioImportId,
                        principalTable: "portfolio_imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    LastLoginAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_changes_Type",
                table: "portfolio_changes",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_rows_PortfolioImportId",
                table: "portfolio_rows",
                column: "PortfolioImportId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_rows");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_portfolio_changes_Type",
                table: "portfolio_changes");

            migrationBuilder.DropColumn(
                name: "IsFirstImport",
                table: "portfolio_imports");
        }
    }
}
