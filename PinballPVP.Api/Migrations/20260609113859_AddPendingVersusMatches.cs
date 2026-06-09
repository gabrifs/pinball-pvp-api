using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinballPVP.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingVersusMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_versus_matches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reporter_id = table.Column<int>(type: "integer", nullable: false),
                    min_player_id = table.Column<int>(type: "integer", nullable: false),
                    max_player_id = table.Column<int>(type: "integer", nullable: false),
                    winner_id = table.Column<int>(type: "integer", nullable: false),
                    loser_id = table.Column<int>(type: "integer", nullable: false),
                    winner_final_score = table.Column<int>(type: "integer", nullable: false),
                    winner_rounds_won = table.Column<int>(type: "integer", nullable: false),
                    loser_final_score = table.Column<int>(type: "integer", nullable: false),
                    loser_rounds_won = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pending_versus_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_pending_versus_matches_users_loser_id",
                        column: x => x.loser_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pending_versus_matches_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pending_versus_matches_users_winner_id",
                        column: x => x.winner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pending_versus_matches_loser_id",
                table: "pending_versus_matches",
                column: "loser_id");

            migrationBuilder.CreateIndex(
                name: "ix_pending_versus_matches_min_player_id_max_player_id",
                table: "pending_versus_matches",
                columns: new[] { "min_player_id", "max_player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pending_versus_matches_reporter_id",
                table: "pending_versus_matches",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_pending_versus_matches_winner_id",
                table: "pending_versus_matches",
                column: "winner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_versus_matches");
        }
    }
}
