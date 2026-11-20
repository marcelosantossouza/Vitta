using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ImportTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    InsurerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SheetName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    HeaderRow = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByAi = table.Column<bool>(type: "INTEGER", nullable: false),
                    AiNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastUsedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Columns = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_templates_insurers_InsurerId",
                        column: x => x.InsurerId,
                        principalTable: "insurers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_templates_InsurerId",
                table: "import_templates",
                column: "InsurerId");

            migrationBuilder.CreateIndex(
                name: "IX_import_templates_Kind_Name",
                table: "import_templates",
                columns: new[] { "Kind", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_templates");
        }
    }
}
