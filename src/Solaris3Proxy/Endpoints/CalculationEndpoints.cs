using Shiron.Solaris3Proxy.Services;

namespace Shiron.Solaris3Proxy.Endpoints;

/// <summary>
/// HTTP endpoints exposing the latest results produced by the background worker.
/// </summary>
public static class CalculationEndpoints {
    /// <summary>Maps the calculation endpoints under <c>/api/calculations</c>.</summary>
    public static void MapCalculationEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/calculations").WithTags("Calculations");

        group.MapGet("/latest", (ICalculationStore store) =>
            store.Latest is { } snapshot
                ? Results.Ok(snapshot)
                : Results.NoContent())
            .WithName("GetLatestCalculation");

        group.MapGet("/health", (ICalculationStore store) =>
            Results.Ok(new { status = "ok", hasData = store.Latest is not null }))
            .WithName("GetHealth");
    }
}
