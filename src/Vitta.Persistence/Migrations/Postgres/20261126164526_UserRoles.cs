using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitta.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class UserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Usuarios que ja existiam (o administrador inicial) entram como Admin e ativos.
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                schema: "vitta",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "vitta",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntil",
                schema: "vitta",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "vitta",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "vitta",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Admin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                schema: "vitta",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "vitta",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                schema: "vitta",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "vitta",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "vitta",
                table: "users");
        }
    }
}
