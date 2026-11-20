using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleGo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    google_sub = table.Column<string>(type: "text", nullable: false),
                    email_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    email_lookup_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    calendar_preference = table.Column<string>(type: "text", nullable: false, defaultValue: "ROC"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_calendar", "calendar_preference IN ('ROC', 'GREGORIAN')");
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate = table.Column<string>(type: "text", nullable: false),
                    normalized_plate = table.Column<string>(type: "text", nullable: false),
                    brand = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    nickname = table.Column<string>(type: "text", nullable: true),
                    color = table.Column<string>(type: "text", nullable: true),
                    vehicle_type = table.Column<string>(type: "text", nullable: false),
                    displacement_bracket = table.Column<string>(type: "text", nullable: true),
                    manufacture_date = table.Column<DateOnly>(type: "date", nullable: false),
                    original_issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    licence_inspection_date = table.Column<DateOnly>(type: "date", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.CheckConstraint("ck_vehicles_bracket", "(vehicle_type = 'CAR' AND displacement_bracket IS NOT NULL AND displacement_bracket IN ('CAR_LE_500', 'CAR_501_600', 'CAR_601_1200', 'CAR_1201_1800', 'CAR_1801_2400', 'CAR_2401_3000', 'CAR_3001_4200', 'CAR_4201_5400', 'CAR_5401_6600', 'CAR_6601_7800', 'CAR_GT_7800', 'CAR_ELECTRIC')) OR (vehicle_type = 'FUEL_MC' AND displacement_bracket IS NOT NULL AND displacement_bracket IN ('MC_LE_50', 'MC_51_250')) OR (vehicle_type = 'HEAVY_MC' AND displacement_bracket IS NOT NULL AND displacement_bracket IN ('MC_251_550', 'MC_GT_550')) OR (vehicle_type = 'ELECTRIC_MC' AND displacement_bracket IS NULL)");
                    table.CheckConstraint("ck_vehicles_brand_length", "char_length(brand) BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_vehicles_color_length", "color IS NULL OR char_length(color) <= 50");
                    table.CheckConstraint("ck_vehicles_dates", "original_issue_date >= manufacture_date");
                    table.CheckConstraint("ck_vehicles_licence_date", "licence_inspection_date IS NULL OR vehicle_type IN ('CAR', 'HEAVY_MC')");
                    table.CheckConstraint("ck_vehicles_model_length", "char_length(model) BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_vehicles_nickname_length", "nickname IS NULL OR char_length(nickname) <= 50");
                    table.CheckConstraint("ck_vehicles_normalized_plate_length", "char_length(normalized_plate) >= 1");
                    table.CheckConstraint("ck_vehicles_plate_length", "char_length(plate) BETWEEN 1 AND 16");
                    table.CheckConstraint("ck_vehicles_type", "vehicle_type IN ('CAR', 'FUEL_MC', 'HEAVY_MC', 'ELECTRIC_MC')");
                    table.ForeignKey(
                        name: "fk_vehicles_owner",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_email_lookup_hash",
                table: "users",
                column: "email_lookup_hash");

            migrationBuilder.CreateIndex(
                name: "ux_users_google_sub",
                table: "users",
                column: "google_sub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_owner_deleted",
                table: "vehicles",
                columns: new[] { "owner_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_owner_plate",
                table: "vehicles",
                columns: new[] { "owner_id", "normalized_plate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
