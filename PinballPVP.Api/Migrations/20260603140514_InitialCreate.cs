using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinballPVP.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "text", nullable: false),
                    nickname = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    winner_id = table.Column<int>(type: "integer", nullable: false),
                    winner_final_score = table.Column<int>(type: "integer", nullable: false),
                    winner_rounds_won = table.Column<int>(type: "integer", nullable: false),
                    loser_id = table.Column<int>(type: "integer", nullable: false),
                    loser_final_score = table.Column<int>(type: "integer", nullable: false),
                    loser_rounds_won = table.Column<int>(type: "integer", nullable: false),
                    played_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_matches_users_loser_id",
                        column: x => x.loser_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_users_winner_id",
                        column: x => x.winner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_records",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    solo_wins = table.Column<int>(type: "integer", nullable: false),
                    solo_losses = table.Column<int>(type: "integer", nullable: false),
                    solo_highscore = table.Column<int>(type: "integer", nullable: false),
                    versus_wins = table.Column<int>(type: "integer", nullable: false),
                    versus_losses = table.Column<int>(type: "integer", nullable: false),
                    versus_highscore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_records", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_player_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_matches_loser_id",
                table: "matches",
                column: "loser_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_winner_id",
                table: "matches",
                column: "winner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "player_records");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
