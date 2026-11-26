using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class BrokerSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "broker_site",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    About = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Hours = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Instagram = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Facebook = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LinkedIn = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Susep = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Cnpj = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ProductsText = table.Column<string>(type: "TEXT", maxLength: 1100, nullable: true),
                    AccentColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broker_site", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "site_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_images", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_images_Kind",
                table: "site_images",
                column: "Kind",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "broker_site");

            migrationBuilder.DropTable(
                name: "site_images");
        }
    }
}
