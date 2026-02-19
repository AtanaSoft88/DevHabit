using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DevHabit.Api.Middleware;

public sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // If the exception is not a ValidationException, we return false to let other handlers try to handle it.
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        // We set the response status code to 400 Bad Request to indicate that the request was invalid due to validation errors.
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        // If the exception is a ValidationException, we create a ProblemDetails response with the validation errors and return it to the client.
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {                
                Detail = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest
            }
        };

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key.ToLowerInvariant(),
                g => g.Select(e => e.ErrorMessage).ToArray());

        // We add the validation errors to the ProblemDetails extensions under the "errors" key, where the value is a dictionary that maps the property names to an array of error messages for that property.
        context.ProblemDetails.Extensions.Add("errors", errors);

        // We add the validation errors to the ProblemDetails extensions for better traceability and to provide more information about the validation errors to the client.
        return await problemDetailsService.TryWriteAsync(context);
    }
}
