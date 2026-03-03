using System.Dynamic;
using System.Linq.Dynamic.Core;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.DTOs.Tags;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;


namespace DevHabit.Api.Controllers;

[ApiController]
[Route("habits")]
public sealed class HabitsController(ApplicationDbContext dbContext, LinkService linkService) : ControllerBase
{
    [HttpGet()]
    public async Task<IActionResult> GetHabits(
        [FromQuery] HabitsQueryParameters query,
        SortMappingProvider sortMappingProvider,
        DataShapingService dataShapingService
        )
    {
        // Validate the 'Sort' query parameter using the SortMappingProvider to ensure it corresponds to valid sorting options for HabitDto and Habit entities.
        if (!sortMappingProvider.ValidateMappings<HabitDto, Habit>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");

        }

        // Validate the 'Fields' query parameter using the DataShapingService to ensure it corresponds to valid fields for data shaping of HabitDto objects.
        if (!dataShapingService.Validate<HabitDto>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided data shaping fields aren't valid: '{query.Fields}'");
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

        int totalCount = await habitsQuery.CountAsync();

        // Pagination applied to the query based on Page and PageSize parameters, retrieving only the relevant subset of HabitDto objects for the requested page.
        List<HabitDto> habits = await habitsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        // Data shaping applied to the resulting list of HabitDto objects based on the Fields query parameter, allowing clients to specify which fields they want in the response.
        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Items = dataShapingService.ShapeCollectionData(habits, query.Fields),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };

        return Ok(paginationResult);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetHabitById(
        string id,
        string? fields,
        DataShapingService dataShapingService)
    {

        if (!dataShapingService.Validate<HabitWithTagsDto>(fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided data shaping fields aren't valid: '{fields}'");
        }

        HabitWithTagsDto? habitWithTagsDto = await dbContext.Habits
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToHabitWithTagsDto())
            .FirstOrDefaultAsync();

        if (habitWithTagsDto is null)
        {
            return NotFound();
        }

        ExpandoObject shapedhabitDto = dataShapingService.ShapeData(habitWithTagsDto, fields);

        LinkDto[] links = CreateLinksForHabit(id, fields);

        shapedhabitDto.TryAdd("links", links);

        return Ok(shapedhabitDto);
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

    //Helper method to create HATEOAS links for a habit resource, including links for retrieving, updating, partially updating, and deleting the habit based on its ID and optional fields for data shaping.
    private LinkDto[] CreateLinksForHabit(string id, string? fields)
    {
        LinkDto[] links =
        [
            linkService.Create(nameof(GetHabitById), "self", HttpMethods.Get, new { id, fields }),
            linkService.Create(nameof(UpdateHabit), "update", HttpMethods.Put, new { id }),
            linkService.Create(nameof(PatchHabit), "partial-update", HttpMethods.Patch, new { id }),
            linkService.Create(nameof(DeleteHabit), "delete", HttpMethods.Delete, new { id })
        ];
        return links;
    }
}
