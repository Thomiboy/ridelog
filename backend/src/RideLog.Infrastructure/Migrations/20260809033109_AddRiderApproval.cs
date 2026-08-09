using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRiderApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Approval",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Approval",
                table: "AspNetUsers");
        }
    }
}
