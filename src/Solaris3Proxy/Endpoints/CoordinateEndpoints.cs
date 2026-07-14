using Microsoft.Extensions.Options;
using Shiron.Solaris3Proxy.Options;
using Shiron.Solaris3Proxy.Services;

namespace Shiron.Solaris3Proxy.Endpoints;

/// <summary>
/// HTTP endpoints for coordinate extraction: the live value from the continuous screen capture,
/// and a one-off extraction from an uploaded image (useful for testing the pipeline).
/// </summary>
public static class CoordinateEndpoints {
    /// <summary>Maps the coordinate endpoints under <c>/api/coordinates</c>.</summary>
    public static void MapCoordinateEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/coordinates").WithTags("Coordinates");

        group.MapGet("/latest", (ICoordinateStore store) =>
                store.Latest is { } snapshot
                    ? Results.Ok(snapshot)
                    : Results.NoContent())
            .WithName("GetLatestCoordinate");

        group.MapGet("/latest/image", (ICoordinateStore store) =>
                store.LatestImage is { } png
                    ? Results.File(png, "image/png")
                    : Results.NoContent())
            .WithName("GetLatestImage");

        group.MapPost("/extract", async (
                IFormFile image,
                ICoordinateExtractor extractor,
                IOptions<CoordinateExtractionOptions> options,
                CancellationToken cancellationToken) => {
                    if (image.Length == 0)
                        return Results.BadRequest(new { error = "No image was uploaded." });

                    if (image.Length > options.Value.MaxUploadBytes)
                        return Results.BadRequest(new { error = "Uploaded image exceeds the maximum allowed size." });

                    using var memory = new MemoryStream();
                    await image.CopyToAsync(memory, cancellationToken);

                    var result = extractor.Extract(memory.ToArray());

                    return result.Success
                        ? Results.Ok(result)
                        : Results.UnprocessableEntity(result);
                })
            .DisableAntiforgery()
            .WithName("ExtractCoordinates");
    }
}
