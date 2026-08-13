using Habit_Tracker.Domain.Entities;
using Habit_Tracker.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habit_Tracker.Persistence.Configurations;

public class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.Property(h => h.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(h => h.Unit).HasMaxLength(50);
        builder.Property(h => h.UnitPlural).HasMaxLength(50);

        // Restrict (not Cascade) so deleting a parent habit doesn't silently cascade-delete
        // its sub-habits' entry history; the app layer decides how to handle that.
        builder.HasOne(h => h.ParentHabit)
            .WithMany(h => h.SubHabits)
            .HasForeignKey(h => h.ParentHabitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.UserId);
    }
}
