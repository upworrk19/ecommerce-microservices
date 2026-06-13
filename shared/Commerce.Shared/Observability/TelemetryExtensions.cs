using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Commerce.Shared.Observability;

public static class TelemetryExtensions
{
    /// <summary>
    /// Wires OpenTelemetry tracing + metrics with OTLP export. ASP.NET, HttpClient,
    /// EF Core and MassTransit all emit Activities into the same trace, so a single
    /// trace id flows across HTTP hops AND the RabbitMQ broker hop.
    /// </summary>
    public static WebApplicationBuilder AddCommerceObservability(
        this WebApplicationBuilder builder, string serviceName)
    {
        var otlpEndpoint = builder.Configuration["Otlp:Endpoint"]
                           ?? "http://localhost:4317";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("MassTransit")            // MassTransit's built-in ActivitySource
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                }));

        return builder;
    }
}
