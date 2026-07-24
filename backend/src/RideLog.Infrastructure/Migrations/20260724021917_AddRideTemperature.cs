using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRideTemperature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageTemperatureCelsius",
                table: "Rides",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxTemperatureCelsius",
                table: "Rides",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinTemperatureCelsius",
                table: "Rides",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageTemperatureCelsius",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "MaxTemperatureCelsius",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "MinTemperatureCelsius",
                table: "Rides");
        }
    }
}
