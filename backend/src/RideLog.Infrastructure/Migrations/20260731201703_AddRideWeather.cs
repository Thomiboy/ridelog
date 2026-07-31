using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRideWeather : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Weather",
                table: "Rides",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeatherOutcome",
                table: "Rides",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Weather",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "WeatherOutcome",
                table: "Rides");
        }
    }
}
