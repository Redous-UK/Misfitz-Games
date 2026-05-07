using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Misfitz_Games.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBattleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Tournaments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndsAtUtc",
                table: "Tournaments",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Prize",
                table: "Tournaments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredSignups",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartsAtUtc",
                table: "Tournaments",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Tournaments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Tournaments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "OpponentName",
                table: "BattleEvents",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "BattleEvents",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "BattleEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RoomRef",
                table: "BattleEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "BattleEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "TournamentSignups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SignedUpAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentSignups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentSignups_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentSignups_TournamentId_UserId",
                table: "TournamentSignups",
                columns: new[] { "TournamentId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentSignups");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "EndsAtUtc",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Prize",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "RequiredSignups",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "StartsAtUtc",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "BattleEvents");

            migrationBuilder.DropColumn(
                name: "RoomRef",
                table: "BattleEvents");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "BattleEvents");

            migrationBuilder.AlterColumn<string>(
                name: "OpponentName",
                table: "BattleEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "BattleEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
