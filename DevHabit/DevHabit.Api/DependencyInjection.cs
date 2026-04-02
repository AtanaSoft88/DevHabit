using System.Net.Http.Headers;
using System.Text;
using Asp.Versioning;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Middleware;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using DevHabit.Api.Settings;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.IdentityModel.Tokens;
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
        // Configure the application database context to use PostgreSQL with the connection string from the configuration, and specify the migrations history table to be in the application's schema for better organization of migration history
        builder.Services.AddDbContext<ApplicationDbContext>(opt =>

         opt
            .UseNpgsql(
               builder.Configuration.GetConnectionString("PostgresDatabase"),
               npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
            .UseSnakeCaseNamingConvention());

        // Configure the identity database context to use PostgreSQL with the same connection string and specify a different migrations history table and schema for identity-related migrations, ensuring that the identity data is stored separately from the application data while still using the same database
        builder.Services.AddDbContext<ApplicationIdentityDbContext>(opt =>

         opt
            .UseNpgsql(
               builder.Configuration.GetConnectionString("PostgresDatabase"),
               npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Identity))
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
        // Register the token provider as a transient service to handle the generation of JWT tokens for authentication purposes, allowing the application to issue tokens based on the configured JWT settings
        builder.Services.AddTransient<TokenProvider>();

        // Register the in-memory cache service to enable caching of data within the application, which can improve performance by reducing the need to repeatedly fetch or compute data that doesn't change frequently
        builder.Services.AddMemoryCache();

        //Access within the current request scope, which can be used to store and retrieve user-specific information such as the user's identity, roles, or other context data that may be needed across different services during the processing of a request
        builder.Services.AddScoped<UserContext>();

        // Register the GitHub access token service as a scoped service to manage the retrieval and caching of GitHub access tokens for API requests, allowing the application to authenticate with the GitHub API and make authorized requests on behalf of the user
        builder.Services.AddScoped<GitHubAccessTokenService>();
        builder.Services.AddTransient<GitHubService>();
        builder.Services
            .AddHttpClient("github")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.github.com");

                client.DefaultRequestHeaders
                    .UserAgent.Add(new ProductInfoHeaderValue("DevHabit", "1.0"));

                client.DefaultRequestHeaders
                    .Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            });

        return builder;
    }

    public static WebApplicationBuilder AddAuthenticationServices(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>();

        // Configure the JwtAuthOptions by binding it to the "Jwt" section of the configuration, allowing the application to easily access JWT-related settings such as issuer, audience, key, and expiration times from the configuration file
        //This IOptions pattern allows us to inject IOptions<JwtAuthOptions> into our services and controllers to access the JWT settings in a strongly-typed manner
        builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection("Jwt"));

        // Retrieve the JwtAuthOptions from the configuration to use its values for configuring JWT Bearer authentication, ensuring that the token validation parameters are set according to the application's JWT settings
        JwtAuthOptions jwtAuthOptions = builder.Configuration.GetSection("Jwt").Get<JwtAuthOptions>()!;

        // Configure JWT Bearer authentication with the specified token validation parameters, including the valid issuer, audience, and signing key based on the values from the JwtAuthOptions, enabling the application to authenticate and validate JWT tokens for secure access to protected resources
        builder.Services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                        })
                        .AddJwtBearer(opt =>
                        {
                            opt.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidIssuer = jwtAuthOptions.Issuer,
                                ValidAudience = jwtAuthOptions.Audience,
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtAuthOptions.Key)),
                            };
                        });

        builder.Services.AddAuthorization();

        return builder;
    }
}
