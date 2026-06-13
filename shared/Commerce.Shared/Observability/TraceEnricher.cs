using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Commerce.Shared.Observability;

/// <summary>Stamps every log event with the current TraceId/SpanId so logs in Seq
/// can be filtered to one distributed request across all services.</summary>
public sealed class TraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var activity = Activity.Current;
        if (activity is null) return;
        logEvent.AddPropertyIfAbsent(factory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(factory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}
