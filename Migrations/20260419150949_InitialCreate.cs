using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Misfitz_Games.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    Capability = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalDeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalSwitchCode = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxPulseSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Effects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Effects", x => x.Id);
                });

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
                name: "Riddles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Question = table.Column<string>(type: "TEXT", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Riddles", x => x.Id);
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
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActiveUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DefaultGame = table.Column<string>(type: "TEXT", nullable: false),
                    AutoRestore = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowGuests = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverlaysEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TikTokLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TikTokOpenId = table.Column<string>(type: "TEXT", nullable: false),
                    TikTokUsername = table.Column<string>(type: "TEXT", nullable: true),
                    AccessTokenEnc = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshTokenEnc = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenExpiresUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Scopes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TikTokLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TuyaLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TuyaUid = table.Column<string>(type: "TEXT", nullable: false),
                    ApiBase = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenEnc = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshTokenEnc = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenExpiresUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TuyaLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserIdMaps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserGuid = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PasswordSalt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Bio = table.Column<string>(type: "TEXT", nullable: true),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsProfilePublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowAvatarInRoom = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowOnlineStatus = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmailAlerts = table.Column<bool>(type: "INTEGER", nullable: false),
                    SecurityAlerts = table.Column<bool>(type: "INTEGER", nullable: false),
                    GameReminders = table.Column<bool>(type: "INTEGER", nullable: false),
                    DigestFrequency = table.Column<string>(type: "TEXT", nullable: false),
                    Timezone = table.Column<string>(type: "TEXT", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    Accent = table.Column<string>(type: "TEXT", nullable: false),
                    CompactLayout = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowTips = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublicRoomListing = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowGameplayStats = table.Column<bool>(type: "INTEGER", nullable: false),
                    HomeRoomCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastLoginUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceGroupMembers",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceGroupMembers", x => new { x.GroupId, x.DeviceId });
                    table.ForeignKey(
                        name: "FK_DeviceGroupMembers_DeviceGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DeviceGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceGroupMembers_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EffectTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EffectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetType = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DurationSecondsOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EffectTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EffectTargets_DeviceGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DeviceGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EffectTargets_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EffectTargets_Effects_EffectId",
                        column: x => x.EffectId,
                        principalTable: "Effects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_DeviceGroupMembers_DeviceId",
                table: "DeviceGroupMembers",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceGroups_OwnerUserId_Name",
                table: "DeviceGroups",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OwnerUserId_Name",
                table: "Devices",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OwnerUserId_Provider_ExternalDeviceId",
                table: "Devices",
                columns: new[] { "OwnerUserId", "Provider", "ExternalDeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Effects_OwnerUserId_Name",
                table: "Effects",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EffectTargets_DeviceId",
                table: "EffectTargets",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_EffectTargets_EffectId",
                table: "EffectTargets",
                column: "EffectId");

            migrationBuilder.CreateIndex(
                name: "IX_EffectTargets_GroupId",
                table: "EffectTargets",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RiddleCatalog_Id",
                table: "RiddleCatalog",
                column: "Id",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_OwnerUserId_Code",
                table: "Rooms",
                columns: new[] { "OwnerUserId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TikTokLinks_UserId",
                table: "TikTokLinks",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TuyaLinks_UserId",
                table: "TuyaLinks",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserIdMaps_UserGuid",
                table: "UserIdMaps",
                column: "UserGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceGroupMembers");

            migrationBuilder.DropTable(
                name: "EffectTargets");

            migrationBuilder.DropTable(
                name: "RiddlePlayerStats");

            migrationBuilder.DropTable(
                name: "Riddles");

            migrationBuilder.DropTable(
                name: "RiddleSubmission");

            migrationBuilder.DropTable(
                name: "RoomPlayerScores");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "TikTokLinks");

            migrationBuilder.DropTable(
                name: "TuyaLinks");

            migrationBuilder.DropTable(
                name: "UserIdMaps");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "DeviceGroups");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Effects");

            migrationBuilder.DropTable(
                name: "RiddleRound");

            migrationBuilder.DropTable(
                name: "RiddleCatalog");
        }
    }
}
