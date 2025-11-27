using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bookify.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedingData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.InsertData(
                table: "RoomTypes",
                columns: new[] { "Id", "Description", "ImageUrl", "MaxOccupancy", "Name", "PricePerNight" },
                values: new object[,]
                {
                    { 1, "Comfortable single room with one bed, perfect for solo travelers.", "Bookify.Web\\wwwroot\\images\\G1.jpg", 1, "Single Room", 2000.00m },
                    { 2, "Spacious double room with two beds, ideal for couples or friends.", "Bookify.Web\\wwwroot\\images\\G2.jpg", 2, "Double Room", 3500.00m },
                    { 3, "Luxurious suite with separate living area and premium amenities.", "Bookify.Web\\wwwroot\\images\\G3.jpg", 4, "Deluxe Suite", 10000.00m },
                    { 4, "Large family room with multiple beds, perfect for families.", "Bookify.Web\\wwwroot\\images\\G4.jpg", 5, "Family Room", 50000.00m },
                    { 5, "Ultra-luxurious presidential suite with all premium features.", "Bookify.Web\\wwwroot\\images\\G55.jpg", 6, "Presidential Suite", 20000.00m }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "IsAvailable", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[,]
                {
                    { 1, true, "Ground floor, near elevator", "101", 1 },
                    { 2, true, "Ground floor, quiet area", "102", 1 }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[] { 3, "Under maintenance", "103", 1 });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "IsAvailable", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[,]
                {
                    { 4, true, "Second floor, city view", "201", 1 },
                    { 5, true, "Second floor, garden view", "202", 1 },
                    { 6, true, "Third floor, balcony", "301", 2 },
                    { 7, true, "Third floor, sea view", "302", 2 }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[] { 8, "Currently occupied", "303", 2 });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "IsAvailable", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[,]
                {
                    { 9, true, "Third floor, corner room", "304", 2 },
                    { 10, true, "Third floor, premium view", "305", 2 },
                    { 11, true, "Fourth floor, luxury suite", "401", 3 },
                    { 12, true, "Fourth floor, jacuzzi included", "402", 3 }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[] { 13, "Reserved for VIP", "403", 3 });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "IsAvailable", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[,]
                {
                    { 14, true, "Fifth floor, panoramic view", "501", 3 },
                    { 15, true, "Sixth floor, family friendly", "601", 4 },
                    { 16, true, "Sixth floor, connecting rooms available", "602", 4 }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[] { 17, "Currently booked", "603", 4 });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "IsAvailable", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[,]
                {
                    { 18, true, "Seventh floor, presidential suite", "701", 5 },
                    { 19, true, "Seventh floor, exclusive access", "702", 5 }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "Notes", "RoomNumber", "RoomTypeId" },
                values: new object[] { 20, "Under renovation", "703", 5 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "RoomTypes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "RoomTypes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "RoomTypes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "RoomTypes",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "RoomTypes",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });
        }
    }
}
