using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.Shared.Errors;

public static class ProblemDetailsExtensions
{
    public static WebApplicationBuilder AddCommerceProblemDetails(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        return builder;
    }
}
