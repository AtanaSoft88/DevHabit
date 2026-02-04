using DevHabit.Api.Entities;

namespace DevHabit.Api.DTOs.Habits;
internal static class HabitMappings
{    
    public static HabitDto ToDto(this Habit habit)
    {
        return new HabitDto
        {
            Id = habit.Id,
            Name = habit.Name,
            Description = habit.Description,
            Type = habit.Type,
            Frequency = new FrequencyDto
            {
                Type = habit.Frequency.Type,
                TimesPerPeriod = habit.Frequency.TimesPerPeriod
            },
            Target = new TargetDto
            {
                Value = habit.Target.Value,
                Unit = habit.Target.Unit
            },
            Status = habit.Status,
            IsArchived = habit.IsArchived,
            EndDate = habit.EndDate,
            Milestone = habit.Milestone == null ? null : new MilestoneDto
            {
                Target = habit.Milestone.Target,
                Current = habit.Milestone.Current
            },
            CreatedAtUtc = habit.CreatedAtUtc,
            UpdatedAtUtc = habit.UpdatedAtUtc,
            LastCompletedAtUtc = habit.LastCompletedAtUtc
        };
    }
    public static Habit ToEntity(this CreateHabitDto dto)
    {
        Habit habit = new()
        {
            Id = $"h_{Guid.CreateVersion7()}",
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            Frequency = new Frequency
            {
                Type = dto.Frequency.Type,
                TimesPerPeriod = dto.Frequency.TimesPerPeriod
            },
            Target = new Target 
            { 
                Value = dto.Target.Value,
                Unit = dto.Target.Unit
            },
            Status = HabitStatus.OnGoing,
            IsArchived = false,
            EndDate = dto.EndDate,
            Milestone = dto.Milestone is not null 
            ? new Milestone
            {
                 Target = dto.Milestone.Target,
                 Current = 0 // Initialize current progress to 0
            } : null,
            CreatedAtUtc = DateTime.UtcNow
        };

        return habit;
    }

    public static void UpdateFromDto(this Habit habit, UpdateHabitDto dto) 
    {
        habit.Name = dto.Name;
        habit.Description = dto.Description;
        habit.Type = dto.Type;       
        habit.EndDate = dto.EndDate;

        // Update frequency (assuming it's immutable, create a new instance)
        habit.Frequency = new Frequency
        {
            Type = dto.Frequency.Type,
            TimesPerPeriod = dto.Frequency.TimesPerPeriod
        };

        // Update target 
        habit.Target = new Target
        {
            Value = dto.Target.Value,
            Unit = dto.Target.Unit
        };

        //Update milestone if provided, otherwise set to null
        if (dto.Milestone != null)
        {
            habit.Milestone ??= new Milestone(); // Create a new Milestone if it doesn't exist
            habit.Milestone.Target = dto.Milestone.Target;
            // Note: We do not update Current here to preserve progress
        }

        habit.UpdatedAtUtc = DateTime.UtcNow; // Update the timestamp for when the habit was last updated
    }
}
