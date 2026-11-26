using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class BrokerSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "broker_site",
                schema: "vitta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Host = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    About = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Hours = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Instagram = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Facebook = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LinkedIn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Susep = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ProductsText = table.Column<string>(type: "character varying(1100)", maxLength: 1100, nullable: true),
                    AccentColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broker_site", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "site_images",
                schema: "vitta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_images", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_images_Kind",
                schema: "vitta",
                table: "site_images",
                column: "Kind",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "broker_site",
                schema: "vitta");

            migrationBuilder.DropTable(
                name: "site_images",
                schema: "vitta");
        }
    }
}
