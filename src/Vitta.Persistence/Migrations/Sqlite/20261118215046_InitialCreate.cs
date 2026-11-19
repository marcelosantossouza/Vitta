using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SearchName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Cpf = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "insurers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insurers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prospects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    NextFollowUpOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ConvertedClientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InsurerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PolicyNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    MonthlyPremium = table.Column<double>(type: "REAL", precision: 12, scale: 2, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EndedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EndReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policies_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policies_insurers_InsurerId",
                        column: x => x.InsurerId,
                        principalTable: "insurers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InsurerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceMonth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio_imports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_portfolio_imports_insurers_InsurerId",
                        column: x => x.InsurerId,
                        principalTable: "insurers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PolicyNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    HolderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HolderCpf = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PortfolioImportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_portfolio_changes_portfolio_imports_PortfolioImportId",
                        column: x => x.PortfolioImportId,
                        principalTable: "portfolio_imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "insurers",
                columns: new[] { "Id", "Code", "CreatedAt", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("0199a000-0000-7000-8000-000000000001"), "MAG", 1308729802752000000L, true, "MAG Seguros (Mongeral Aegon)", null },
                    { new Guid("0199a000-0000-7000-8000-000000000002"), "PRUDENTIAL", 1308729802752000000L, true, "Prudential do Brasil", null },
                    { new Guid("0199a000-0000-7000-8000-000000000003"), "ICATU", 1308729802752000000L, true, "Icatu Seguros", null },
                    { new Guid("0199a000-0000-7000-8000-000000000004"), "METLIFE", 1308729802752000000L, true, "MetLife", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_clients_Cpf",
                table: "clients",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_SearchName",
                table: "clients",
                column: "SearchName");

            migrationBuilder.CreateIndex(
                name: "IX_insurers_Code",
                table: "insurers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_policies_ClientId",
                table: "policies",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_policies_InsurerId_PolicyNumber",
                table: "policies",
                columns: new[] { "InsurerId", "PolicyNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_changes_PortfolioImportId",
                table: "portfolio_changes",
                column: "PortfolioImportId");

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_imports_InsurerId_ReferenceMonth",
                table: "portfolio_imports",
                columns: new[] { "InsurerId", "ReferenceMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prospects_Stage",
                table: "prospects",
                column: "Stage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policies");

            migrationBuilder.DropTable(
                name: "portfolio_changes");

            migrationBuilder.DropTable(
                name: "prospects");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "portfolio_imports");

            migrationBuilder.DropTable(
                name: "insurers");
        }
    }
}
