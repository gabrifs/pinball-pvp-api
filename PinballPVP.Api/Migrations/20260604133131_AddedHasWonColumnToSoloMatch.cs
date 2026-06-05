using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PinballPVP.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddedHasWonColumnToSoloMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_won",
                table: "solo_matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_won",
                table: "solo_matches");
        }
    }
}
