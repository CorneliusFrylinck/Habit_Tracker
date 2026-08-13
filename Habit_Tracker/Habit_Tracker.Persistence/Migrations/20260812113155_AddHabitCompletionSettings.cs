using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habit_Tracker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitCompletionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletionMethod",
                table: "Habits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetValue",
                table: "Habits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Habits",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitPlural",
                table: "Habits",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionMethod",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "TargetValue",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "UnitPlural",
                table: "Habits");
        }
    }
}
