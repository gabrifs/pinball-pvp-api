using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinballPVP.Api.Migrations
{
    /// <inheritdoc />
    public partial class SplitMatchesIntoSoloAndVersus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.CreateTable(
                name: "solo_matches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    final_score = table.Column<int>(type: "integer", nullable: false),
                    rounds_won = table.Column<int>(type: "integer", nullable: false),
                    played_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solo_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_solo_matches_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versus_matches",
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
                    table.PrimaryKey("pk_versus_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_versus_matches_users_loser_id",
                        column: x => x.loser_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versus_matches_users_winner_id",
                        column: x => x.winner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_solo_matches_user_id",
                table: "solo_matches",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_versus_matches_loser_id",
                table: "versus_matches",
                column: "loser_id");

            migrationBuilder.CreateIndex(
                name: "ix_versus_matches_winner_id",
                table: "versus_matches",
                column: "winner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solo_matches");

            migrationBuilder.DropTable(
                name: "versus_matches");

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    loser_id = table.Column<int>(type: "integer", nullable: false),
                    winner_id = table.Column<int>(type: "integer", nullable: false),
                    loser_final_score = table.Column<int>(type: "integer", nullable: false),
                    loser_rounds_won = table.Column<int>(type: "integer", nullable: false),
                    played_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    winner_final_score = table.Column<int>(type: "integer", nullable: false),
                    winner_rounds_won = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_matches_loser_id",
                table: "matches",
                column: "loser_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_winner_id",
                table: "matches",
                column: "winner_id");
        }
    }
}
