using DevHabit.Api;
using DevHabit.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddControllers()
       .AddErrorHandling()
       .AddDatabase()
       .AddObservability()
       .AddApplicationServices();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Apply any pending database migrations at startup to ensure the database schema is up to date with the application's data model, which can help prevent issues related to schema mismatches during development.
    await app.ApplyMigrations();
}

app.UseHttpsRedirection();

// Use the global exception handler middleware to catch unhandled exceptions and return ProblemDetails responses
app.UseExceptionHandler();

app.MapControllers();

await app.RunAsync();
