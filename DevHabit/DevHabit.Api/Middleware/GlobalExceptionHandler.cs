using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DevHabit.Api.Middleware;

//Instead of huge information about the exception, we return a generic message to avoid exposing sensitive details about the server or application internals to the client.
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{    
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext 
        { 
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Internal Server Error.",
                Detail = "An error occurred while processing your request. Please try again.",
                Status = StatusCodes.Status500InternalServerError
                
            }
        });
    }
}
