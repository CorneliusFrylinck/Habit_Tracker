using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habit_Tracker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitEntryAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Amount",
                table: "HabitEntries",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "HabitEntries");
        }
    }
}
