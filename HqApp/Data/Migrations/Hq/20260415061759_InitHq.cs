using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HqApp.Data.Migrations.Hq
{
    /// <inheritdoc />
    public partial class InitHq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_salary",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    position = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    monthly_salary = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_salary", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menu",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    cost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "store_policy",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    policy_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    discount_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    opening_hour = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closing_hour = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_policy", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "employee_salary",
                columns: new[] { "id", "created_at", "employee_name", "monthly_salary", "position" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Alice", 55000m, "Manager" },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bob", 32000m, "Barista" }
                });

            migrationBuilder.InsertData(
                table: "menu",
                columns: new[] { "id", "category", "cost", "is_available", "name", "price", "updated_at" },
                values: new object[,]
                {
                    { 1, "coffee", 25m, true, "Americano", 120m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "coffee", 35m, true, "Latte", 150m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "tea", 20m, true, "Green Tea", 100m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "food", 60m, true, "Cheesecake", 180m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "food", 30m, true, "Croissant", 80m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "store_policy",
                columns: new[] { "id", "closing_hour", "discount_rate", "opening_hour", "policy_name", "updated_at" },
                values: new object[,]
                {
                    { 1, new TimeOnly(22, 0, 0), 0m, new TimeOnly(8, 0, 0), "weekday", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new TimeOnly(23, 0, 0), 0.1m, new TimeOnly(9, 0, 0), "weekend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_salary");

            migrationBuilder.DropTable(
                name: "menu");

            migrationBuilder.DropTable(
                name: "store_policy");
        }
    }
}
