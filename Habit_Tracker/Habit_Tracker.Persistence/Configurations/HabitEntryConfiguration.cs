using Habit_Tracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habit_Tracker.Persistence.Configurations;

public class HabitEntryConfiguration : IEntityTypeConfiguration<HabitEntry>
{
    public void Configure(EntityTypeBuilder<HabitEntry> builder)
    {
        builder.HasOne(e => e.Habit)
            .WithMany(h => h.Entries)
            .HasForeignKey(e => e.HabitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.TrackedAt);
    }
}
