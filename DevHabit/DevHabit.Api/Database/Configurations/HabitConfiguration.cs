using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHabit.Api.Database.Configurations;

public sealed class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)            
                    .HasMaxLength(500);
        builder.Property(h => h.UserId)
                    .HasMaxLength(500);
        builder.Property(h => h.Name)                    
                    .HasMaxLength(100);
        builder.Property(h => h.Description)
                    .HasMaxLength(500);
        builder.OwnsOne(f => f.Frequency);

        builder.OwnsOne(t => t.Target, targetBuilder => 
        {
            targetBuilder.Property(t => t.Unit).HasMaxLength(50);
        });

        builder.OwnsOne(m => m.Milestone);

        builder.HasMany(h => h.Tags)
            .WithMany()
            .UsingEntity<HabitTag>();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(h => h.UserId);
            
    }
}
