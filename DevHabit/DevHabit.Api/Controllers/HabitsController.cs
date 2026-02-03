using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("habits")]
public sealed class HabitsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet()]
    public async Task<ActionResult<HabitsCollectionDto>> GetHabits()
    {
        List<HabitDto> habits = await dbContext.Habits
            .Select(habit => new HabitDto
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
            })
            .ToListAsync();

        var habitsCollectionDto = new HabitsCollectionDto
        {
            Data = habits
        };

        return Ok(habitsCollectionDto);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HabitDto>> GetHabitById(string id)
    {
        HabitDto? habit = await dbContext.Habits
            .Where(habit => habit.Id == id)
            .Select(habit => new HabitDto
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
            })
            .FirstOrDefaultAsync();

         return habit is null ?
                      NotFound() :
                      Ok(habit);
    }
}
