using System.Linq.Expressions;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DevHabit.Api.DTOs.Habits.HabitMappings;

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("habits")]
public sealed class HabitsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet()]
    public async Task<ActionResult<HabitsCollectionDto>> GetHabits()
    {
        List<HabitDto> habits = await dbContext.Habits
            .Select(HabitQueries.ProjectToDto())
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
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToDto())
            .FirstOrDefaultAsync();

        return habit is null ?
                     NotFound() :
                     Ok(habit);
    }    

    [HttpPost]
    public async Task<ActionResult<HabitDto>> CreateHabit(CreateHabitDto createHabitDto)
    {
        Habit habit = createHabitDto.ToEntity();

        dbContext.Habits.Add(habit);

        await dbContext.SaveChangesAsync();

        HabitDto habitDto = habit.ToDto();

        return CreatedAtAction(nameof(GetHabitById), new {id = habitDto.Id }, habitDto);
    }
}
