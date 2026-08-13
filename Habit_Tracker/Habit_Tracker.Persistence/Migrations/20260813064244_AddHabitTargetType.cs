using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habit_Tracker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitTargetType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetType",
                table: "Habits",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "Habits");
        }
    }
}
