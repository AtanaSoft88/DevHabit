using DevHabit.Api.Database;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task ApplyMigrations(this WebApplication app) 
    {
        // Create a scope to obtain the required services for applying migrations
        using IServiceScope scope = app.Services.CreateScope();

        // Get the ApplicationDbContext and ApplicationIdentityDbContext instances from the service provider
        await using ApplicationDbContext applicationDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using ApplicationIdentityDbContext identityDbContext = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();

        try
        {
            // Apply pending migrations for both contexts
            await applicationDbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Application Database migrations applied successfully.");

            await identityDbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Identity Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "An error occurred while applying database migrations.");
        }
    }
}
