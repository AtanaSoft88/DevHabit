using DevHabit.Api.DTOs.Common;

namespace DevHabit.Api.Services;

public sealed class LinkService(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
{
    public LinkDto Create(
        string endpointName,
        string rel,
        string method,
        object? values = null,
        string? contrller = null) 
    {
        string href  = linkGenerator.GetUriByAction(
            httpContextAccessor.HttpContext!,
            action: endpointName,
            controller: contrller,
            values: values);

        return new LinkDto
        {
            Href = href ?? throw new Exception("Invalid endpoint name provided"),
            Rel = rel,
            Method = method
        };
    }
}
