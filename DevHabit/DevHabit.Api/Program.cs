using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Extensions;
using DevHabit.Api.Middleware;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{    
    options.ReturnHttpNotAcceptable = true; //Added to return 406 when the requested format is not supported    

})
.AddNewtonsoftJson() //Added NewtonsoftJson support
.AddXmlSerializerFormatters(); //Added XmlSerializerFormatters() to support XML format

// Add FluentValidation support from the assembly containing the Program class
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add ProblemDetails support and customize it to include the requestId in the extensions
builder.Services.AddProblemDetails(options => 
{
    options.CustomizeProblemDetails = context =>
    {
        // Add the requestId to the ProblemDetails extensions for better traceability
        context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
    };
});

// Register the validation exception handler middleware to handle FluentValidation exceptions
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

// Register the global exception handler middleware
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(opt =>

    opt
       .UseNpgsql(
         builder.Configuration.GetConnectionString("PostgresDatabase"),
         npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
       .UseSnakeCaseNamingConvention());

// Configure OpenTelemetry for tracing and metrics collection, including instrumentation for HTTP client, ASP.NET Core, and Npgsql, and set up the OTLP exporter to send telemetry data to a compatible backend
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddNpgsql())
    .WithMetrics(metrics => metrics
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation())
    .UseOtlpExporter();

// Configure OpenTelemetry logging to include scopes and formatted messages, which can provide more context and readability in the logs
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
});

// Register the sort mapping provider as a transient service to provide sort mappings for different DTO and entity combinations
builder.Services.AddTransient<SortMappingProvider>();
// Register the sort mapping definition for HabitDto and Habit to enable sorting based on the defined mappings
builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<HabitDto, Habit>>(_ => HabitMappings.SortMapping);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await app.ApplyMigrations();
}

app.UseHttpsRedirection();

// Use the global exception handler middleware to catch unhandled exceptions and return ProblemDetails responses
app.UseExceptionHandler();

app.MapControllers();

await app.RunAsync();
