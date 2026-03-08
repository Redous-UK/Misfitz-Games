using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Misfitz_Games.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomPlayerScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Riddles_IsActive_Category",
                table: "Riddles");

            migrationBuilder.CreateTable(
                name: "RiddleCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Difficulty = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Answer = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    AcceptableAnswersJson = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    HintsJson = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiddleCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiddlePlayerStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TotalPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrectCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiddlePlayerStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomPlayerScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TotalScore = table.Column<int>(type: "INTEGER", nullable: false),
                    TriviaScore = table.Column<int>(type: "INTEGER", nullable: false),
                    HangmanScore = table.Column<int>(type: "INTEGER", nullable: false),
                    HigherLowerScore = table.Column<int>(type: "INTEGER", nullable: false),
                    RiddleScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ContextoScore = table.Column<int>(type: "INTEGER", nullable: false),
                    DealScore = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomPlayerScores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiddleRound",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CatalogRiddleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Question = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Answer = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    HintsJson = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    BasePoints = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeLimitSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevealAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    WinnerUserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    WinnerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    WinnerPoints = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiddleRound", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiddleRound_RiddleCatalog_CatalogRiddleId",
                        column: x => x.CatalogRiddleId,
                        principalTable: "RiddleCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RiddleSubmission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AnswerText = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    PointsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiddleSubmission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiddleSubmission_RiddleRound_RoundId",
                        column: x => x.RoundId,
                        principalTable: "RiddleRound",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiddlePlayerStats_RoomId_UserId",
                table: "RiddlePlayerStats",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiddleRound_CatalogRiddleId",
                table: "RiddleRound",
                column: "CatalogRiddleId");

            migrationBuilder.CreateIndex(
                name: "IX_RiddleRound_RoomCode",
                table: "RiddleRound",
                column: "RoomCode");

            migrationBuilder.CreateIndex(
                name: "IX_RiddleRound_RoomId_Status",
                table: "RiddleRound",
                columns: new[] { "RoomId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RiddleSubmission_RoundId_UserId",
                table: "RiddleSubmission",
                columns: new[] { "RoundId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayerScores_RoomId_UserId",
                table: "RoomPlayerScores",
                columns: new[] { "RoomId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiddlePlayerStats");

            migrationBuilder.DropTable(
                name: "RiddleSubmission");

            migrationBuilder.DropTable(
                name: "RoomPlayerScores");

            migrationBuilder.DropTable(
                name: "RiddleRound");

            migrationBuilder.DropTable(
                name: "RiddleCatalog");

            migrationBuilder.CreateIndex(
                name: "IX_Riddles_IsActive_Category",
                table: "Riddles",
                columns: new[] { "IsActive", "Category" });
        }
    }
}
