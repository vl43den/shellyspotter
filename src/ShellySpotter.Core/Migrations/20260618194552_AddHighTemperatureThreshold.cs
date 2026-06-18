using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShellySpotter.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddHighTemperatureThreshold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "HighTemperatureThreshold",
                table: "Rooms",
                type: "float",
                nullable: false,
                defaultValue: 28.0);

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 1,
                column: "HighTemperatureThreshold",
                value: 28.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighTemperatureThreshold",
                table: "Rooms");
        }
    }
}
