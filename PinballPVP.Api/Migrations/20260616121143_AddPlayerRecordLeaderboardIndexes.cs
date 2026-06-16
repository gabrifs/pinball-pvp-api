using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PinballPVP.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRecordLeaderboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_player_records_solo_highscore",
                table: "player_records",
                column: "solo_highscore");

            migrationBuilder.CreateIndex(
                name: "ix_player_records_solo_wins",
                table: "player_records",
                column: "solo_wins");

            migrationBuilder.CreateIndex(
                name: "ix_player_records_versus_highscore",
                table: "player_records",
                column: "versus_highscore");

            migrationBuilder.CreateIndex(
                name: "ix_player_records_versus_wins",
                table: "player_records",
                column: "versus_wins");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_player_records_solo_highscore",
                table: "player_records");

            migrationBuilder.DropIndex(
                name: "ix_player_records_solo_wins",
                table: "player_records");

            migrationBuilder.DropIndex(
                name: "ix_player_records_versus_highscore",
                table: "player_records");

            migrationBuilder.DropIndex(
                name: "ix_player_records_versus_wins",
                table: "player_records");
        }
    }
}
