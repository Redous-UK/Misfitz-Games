using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Misfitz_Games.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUser",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.Id);
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
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OwnerUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActiveUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_AppUser_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_UserIdMaps_UserGuid",
                table: "UserIdMaps",
                column: "UserGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "TikTokLinks");

            migrationBuilder.DropTable(
                name: "UserIdMaps");

            migrationBuilder.DropTable(
                name: "AppUser");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Users");
        }
    }
}
