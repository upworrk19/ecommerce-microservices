using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Commerce.Shared.Observability;

public static class SerilogExtensions
{
    /// <summary>Configures Serilog to enrich with trace ids and ship to Seq + console.</summary>
    public static WebApplicationBuilder UseCommerceSerilog(
        this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((ctx, cfg) => cfg
            .Enrich.FromLogContext()
            .Enrich.With<TraceEnricher>()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console()
            .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://localhost:5341"));
        return builder;
    }
}
