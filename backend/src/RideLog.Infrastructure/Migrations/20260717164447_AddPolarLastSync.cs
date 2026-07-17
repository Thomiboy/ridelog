using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolarLastSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncAt",
                table: "PolarConnections",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSyncAt",
                table: "PolarConnections");
        }
    }
}
