using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolarLastSyncSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastSyncFailed",
                table: "PolarConnections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSyncImported",
                table: "PolarConnections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSyncSkipped",
                table: "PolarConnections",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSyncFailed",
                table: "PolarConnections");

            migrationBuilder.DropColumn(
                name: "LastSyncImported",
                table: "PolarConnections");

            migrationBuilder.DropColumn(
                name: "LastSyncSkipped",
                table: "PolarConnections");
        }
    }
}
