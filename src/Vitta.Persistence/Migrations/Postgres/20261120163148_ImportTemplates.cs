using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ImportTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_templates",
                schema: "vitta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InsurerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SheetName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    HeaderRow = table.Column<int>(type: "integer", nullable: false),
                    CreatedByAi = table.Column<bool>(type: "boolean", nullable: false),
                    AiNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Columns = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_templates_insurers_InsurerId",
                        column: x => x.InsurerId,
                        principalSchema: "vitta",
                        principalTable: "insurers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_templates_InsurerId",
                schema: "vitta",
                table: "import_templates",
                column: "InsurerId");

            migrationBuilder.CreateIndex(
                name: "IX_import_templates_Kind_Name",
                schema: "vitta",
                table: "import_templates",
                columns: new[] { "Kind", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_templates",
                schema: "vitta");
        }
    }
}
