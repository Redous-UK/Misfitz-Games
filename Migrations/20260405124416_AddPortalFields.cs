using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Misfitz_Games.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiddleRound_RiddleCatalog_CatalogRiddleId",
                table: "RiddleRound");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_AppUser_OwnerUserId",
                table: "Rooms");

            migrationBuilder.DropTable(
                name: "AppUser");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RiddleCatalog",
                table: "RiddleCatalog");

            migrationBuilder.AddColumn<string>(
                name: "Accent",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompactLayout",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DigestFrequency",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EmailAlerts",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GameReminders",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProfilePublic",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PublicRoomListing",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SecurityAlerts",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAvatarInRoom",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowGameplayStats",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnlineStatus",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTips",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Timezone",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerUserId",
                table: "Rooms",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "AllowGuests",
                table: "Rooms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRestore",
                table: "Rooms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DefaultGame",
                table: "Rooms",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Rooms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "Rooms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OverlaysEnabled",
                table: "Rooms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Rooms",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RiddleCatalogs",
                table: "RiddleCatalogs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_RiddleCatalogs_Id",
                table: "RiddleCatalogs",
                column: "Id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RiddleRound_RiddleCatalogs_CatalogRiddleId",
                table: "RiddleRound",
                column: "CatalogRiddleId",
                principalTable: "RiddleCatalogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiddleRound_RiddleCatalogs_CatalogRiddleId",
                table: "RiddleRound");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RiddleCatalogs",
                table: "RiddleCatalogs");

            migrationBuilder.DropIndex(
                name: "IX_RiddleCatalogs_Id",
                table: "RiddleCatalogs");

            migrationBuilder.DropColumn(
                name: "Accent",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompactLayout",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DigestFrequency",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailAlerts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GameReminders",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsProfilePublic",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublicRoomListing",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityAlerts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowAvatarInRoom",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowGameplayStats",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowOnlineStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowTips",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Theme",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Timezone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AllowGuests",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "AutoRestore",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "DefaultGame",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "OverlaysEnabled",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Rooms");

            migrationBuilder.RenameTable(
                name: "RiddleCatalogs",
                newName: "RiddleCatalog");

            migrationBuilder.AlterColumn<long>(
                name: "OwnerUserId",
                table: "Rooms",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RiddleCatalog",
                table: "RiddleCatalog",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_RiddleRound_RiddleCatalog_CatalogRiddleId",
                table: "RiddleRound",
                column: "CatalogRiddleId",
                principalTable: "RiddleCatalog",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_AppUser_OwnerUserId",
                table: "Rooms",
                column: "OwnerUserId",
                principalTable: "AppUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
