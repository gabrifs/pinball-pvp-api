using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinballPVP.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAllTimeBestRecordsAndYearlyLeaderboards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "all_time_best_records",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    solo_highscore = table.Column<int>(type: "integer", nullable: false),
                    solo_highscore_year = table.Column<int>(type: "integer", nullable: true),
                    solo_wins = table.Column<int>(type: "integer", nullable: false),
                    solo_wins_year = table.Column<int>(type: "integer", nullable: true),
                    solo_matches_played = table.Column<int>(type: "integer", nullable: false),
                    solo_matches_played_year = table.Column<int>(type: "integer", nullable: true),
                    versus_highscore = table.Column<int>(type: "integer", nullable: false),
                    versus_highscore_year = table.Column<int>(type: "integer", nullable: true),
                    versus_wins = table.Column<int>(type: "integer", nullable: false),
                    versus_wins_year = table.Column<int>(type: "integer", nullable: true),
                    versus_matches_played = table.Column<int>(type: "integer", nullable: false),
                    versus_matches_played_year = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_all_time_best_records", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_all_time_best_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "yearly_leaderboard_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    year = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    nickname_snapshot = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_yearly_leaderboard_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_yearly_leaderboard_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_yearly_leaderboard_entries_user_id",
                table: "yearly_leaderboard_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_yearly_leaderboard_entries_year_category_rank",
                table: "yearly_leaderboard_entries",
                columns: new[] { "year", "category", "rank" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "all_time_best_records");

            migrationBuilder.DropTable(
                name: "yearly_leaderboard_entries");
        }
    }
}
