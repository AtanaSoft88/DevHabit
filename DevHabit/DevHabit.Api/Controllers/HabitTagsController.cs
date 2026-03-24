using DevHabit.Api.Constants;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.HabitTags;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Controllers;

/// <summary>
/// The HabitTagsController is responsible for managing the tags associated with a habit. It provides endpoints to upsert (add or update) habit tags and delete a specific habit tag. The controller interacts with the database context to perform the necessary operations on the Habit and HabitTag entities.
/// </summary>
/// <param name="dbContext"></param>
[ApiController]
[Route("habits/{habitId}/tags")]
[Authorize(Roles = $"{Roles.Member}")]
public sealed class HabitTagsController(ApplicationDbContext dbContext) : ControllerBase
{
    public static readonly string Name = nameof(HabitTagsController).Replace("Controller", string.Empty);

    //habits/:id/tags/:tagId
    [HttpPut]
    public async Task<ActionResult> UpsertHabitTags(string habitId, UpsertHabitTagsDto upsertHabitTagsDto)
    {
        // Check if the habit exists, based on the provided habitId. If it doesn't exist, return a 404 Not Found response.
        Habit? habit = await dbContext.Habits
            .Include(h => h.HabitTags)
            .FirstOrDefaultAsync(h => h.Id == habitId);
        if (habit is null)
        {
            return NotFound();
        }

        // Compare the current tag IDs associated with the habit to the tag IDs provided in the request. If they are the same, return a 204 No Content response.
        var currentTagIds = habit.HabitTags.Select(ht => ht.TagId).ToHashSet();
        if (currentTagIds.SetEquals(upsertHabitTagsDto.TagIds))
        {
            return NoContent();
        }

        // Validate that all tag IDs provided in the request exist in the database. If any of the tag IDs are invalid, return a 400 Bad Request response.
        List<string> exsistingTagIds = await dbContext
            .Tags
            .Where(t => upsertHabitTagsDto.TagIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();

        if (exsistingTagIds.Count != upsertHabitTagsDto.TagIds.Count)
        {
            return BadRequest("One or more tag IDs are invalid.");
        }

        // Remove any existing habit tags that are not included in the request.
        habit.HabitTags.RemoveAll(ht => !upsertHabitTagsDto.TagIds.Contains(ht.TagId));

        // Add any new habit tags that are included in the request but not currently associated with the habit.
        string[] tagIdsToAdd = upsertHabitTagsDto.TagIds.Except(currentTagIds).ToArray();

        // Add new habit tags to the habit's HabitTags collection. Each new habit tag should include the habitId, tagId, and the current UTC date and time as CreatedAtUtc.
        habit.HabitTags.AddRange(tagIdsToAdd.Select(tagId => new HabitTag 
        { 
            HabitId = habitId, 
            TagId = tagId,
            CreatedAtUtc = DateTime.UtcNow
        }));

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{tagId}")]
    public async Task<ActionResult> DeleteHabitTag(string habitId, string tagId)
    {
        // Check if the habit exists, based on the provided habitId and tagId. If it doesn't exist, return a 404 Not Found response.
        HabitTag? habitTag = await dbContext.HabitTags
            .SingleOrDefaultAsync(ht => ht.HabitId == habitId && ht.TagId == tagId);

        if (habitTag is null)
        {
            return NotFound();
        }

        // Remove the habit tag from the database.
        dbContext.HabitTags.Remove(habitTag);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
