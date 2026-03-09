using Asp.Versioning;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Middleware;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json.Serialization;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DevHabit.Api;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers(options =>
        {
            options.ReturnHttpNotAcceptable = true; //Added to return 406 when the requested format is not supported
        })
            // Configure Newtonsoft.Json to use camel case property names in JSON responses
            .AddNewtonsoftJson(options => options.SerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver())
            .AddXmlSerializerFormatters(); //Added XmlSerializerFormatters() to support XML format

        // Configure the output formatters to support the custom HATEOAS JSON media type, allowing clients to request this format for API responses that include HATEOAS links
        builder.Services.Configure<MvcOptions>(options =>
        {
            NewtonsoftJsonOutputFormatter formatter = options.OutputFormatters
                .OfType<NewtonsoftJsonOutputFormatter>()
                .First();

            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.JsonV1);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.JsonV2);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJson);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJsonV1);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJsonV2);
        });

        builder.Services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1.0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionSelector = new DefaultApiVersionSelector(options);

                options.ApiVersionReader = ApiVersionReader.Combine(
                    new MediaTypeApiVersionReader(),
                    new MediaTypeApiVersionReaderBuilder()
                        .Template("application/vnd.dev-habit.hateoas.{version}+json")
                        .Build());
            })
            .AddMvc();

        builder.Services.AddOpenApi();

        return builder;
    }

    public static WebApplicationBuilder AddErrorHandling(this WebApplicationBuilder builder)
    {
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

        return builder;
    }

    public static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<ApplicationDbContext>(opt =>

         opt
            .UseNpgsql(
               builder.Configuration.GetConnectionString("PostgresDatabase"),
               npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
            .UseSnakeCaseNamingConvention());

        return builder;
    }

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
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

        return builder;
    }

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        // Add FluentValidation support from the assembly containing the Program class
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();

        // Register the sort mapping provider as a transient service to provide sort mappings for different DTO and entity combinations
        builder.Services.AddTransient<SortMappingProvider>();
        // Register the sort mapping definition for HabitDto and Habit to enable sorting based on the defined mappings
        builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<HabitDto, Habit>>(_ => HabitMappings.SortMapping);
        // Register the data shaping service as a transient service to enable shaping of data based on specified fields in the API responses
        builder.Services.AddTransient<DataShapingService>();
        // Register the HTTP context accessor as a singleton service to allow access to the current HTTP context, which can be useful for generating links and accessing request-specific information in services
        builder.Services.AddHttpContextAccessor();
        // Register the link service as a transient service to generate HATEOAS links for API responses, enhancing discoverability and navigation of the API
        builder.Services.AddTransient<LinkService>();
        return builder;

    }
}
