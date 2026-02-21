using System.Linq.Dynamic.Core;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.DTOs.Tags;
using DevHabit.Api.Entities;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;


namespace DevHabit.Api.Controllers;

[ApiController]
[Route("habits")]
public sealed class HabitsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet()]
    public async Task<ActionResult<PaginationResult<HabitDto>>> GetHabits(
        [FromQuery] HabitsQueryParameters query,
        SortMappingProvider sortMappingProvider
        )
    {
        // Validate the 'Sort' query parameter using the SortMappingProvider to ensure it corresponds to valid sorting options for HabitDto and Habit entities.
        if (!sortMappingProvider.ValidateMappings<HabitDto, Habit>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");
                
        }

        // Searching and filtering from Query parameters taken from 'HabitsQueryParameters' class
        query.Search ??= query.Search?.Trim().ToLower();

        SortMapping[] sortMappings = sortMappingProvider.GetMappings<HabitDto, Habit>();

        IQueryable<HabitDto> habitsQuery = dbContext
            .Habits
            //Search parameter matched with Habit.Name or Habit.Description when available
            .Where(h => query.Search == null ||
                        h.Name.ToLower().Contains(query.Search) ||
                        h.Description != null && h.Description.ToLower().Contains(query.Search))
            // Type parameter matched with Habit.Type when available
            .Where(h => query.Type == null || h.Type == query.Type)
            // Status parameter matched with Habit.Status when available
            .Where(h => query.Status == null || h.Status == query.Status)
            // Sorting applied based on Sort parameter and sort mappings provided by SortMappingProvider
            .ApplySort(query.Sort, sortMappings)
            .Select(HabitQueries.ProjectToDto());

        //Pagination applied to the query using the PaginationResult class, which takes care of calculating total count, total pages, and slicing the data based on Page and PageSize parameters.
        PaginationResult<HabitDto> paginationResult = await PaginationResult<HabitDto>.CreateAsync(habitsQuery, query.Page, query.PageSize);

        return Ok(paginationResult);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HabitWithTagsDto>> GetHabitById(string id)
    {
        HabitWithTagsDto? habitWithTagsDto = await dbContext.Habits
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToHabitWithTagsDto())
            .FirstOrDefaultAsync();

        return habitWithTagsDto is null ?
                     NotFound() :
                     Ok(habitWithTagsDto);
    }

    [HttpPost]
    public async Task<ActionResult<HabitDto>> CreateHabit(CreateHabitDto createHabitDto,
                                                          IValidator<CreateHabitDto> validator)
    {   
        // Validation exception handler responsible for converting specific validation exception into Problem details responce.
        await validator.ValidateAndThrowAsync(createHabitDto);

        Habit habit = createHabitDto.ToEntity();

        dbContext.Habits.Add(habit);
        
        await dbContext.SaveChangesAsync();
        
        HabitDto habitDto = habit.ToDto();

        return CreatedAtAction(nameof(GetHabitById), new { id = habitDto.Id }, habitDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateHabit(string id, UpdateHabitDto updateHabitDto)
    {
        Habit? habit = await dbContext.Habits
            .FirstOrDefaultAsync(h => h.Id == id);
        if (habit is null)
        {
            return NotFound();
        }

        habit.UpdateFromDto(updateHabitDto);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }    

    [HttpPatch("{id}")] //Using library 'Microsoft.AspNetCore.JsonPatch' for JSON Patch support
    public async Task<ActionResult> PatchHabit(string id, JsonPatchDocument<HabitDto> patchDocument)
    {
        Habit? habit = await dbContext.Habits
            .FirstOrDefaultAsync(h => h.Id == id);
        if (habit is null)
        {
            return NotFound();
        }

        HabitDto habitDto = habit.ToDto();

        patchDocument.ApplyTo(habitDto, ModelState);

        if (!TryValidateModel(habitDto))
        {
            return ValidationProblem(ModelState);
        }

        habit.Name = habitDto.Name;
        habit.Description = habitDto.Description;
        habit.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteHabit(string id)
    {
        Habit? habit = await dbContext.Habits
            .FirstOrDefaultAsync(h => h.Id == id);
        if (habit is null)
        {
            return NotFound();
        }
        dbContext.Habits.Remove(habit);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}
