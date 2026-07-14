using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Shiron.Solaris3Proxy.Models;
using Shiron.Solaris3Proxy.Options;
using Shiron.Solaris3Proxy.Services;

namespace Shiron.Solaris3Proxy.Endpoints;

/// <summary>
/// HTTP endpoints for coordinate extraction: the live values from the continuous screen capture,
/// and a one-off extraction from an uploaded image (useful for testing the pipeline).
/// Handlers use typed results so response schemas appear in OpenAPI (and the generated SDK).
/// </summary>
public static class CoordinateEndpoints {
    /// <summary>Maps the coordinate endpoints under <c>/api/coordinates</c>.</summary>
    public static void MapCoordinateEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/coordinates").WithTags("Coordinates");

        group.MapGet("/latest", Results<Ok<CoordinateSnapshot>, NoContent> (ICoordinateStore store) =>
                store.Latest is { } snapshot
                    ? TypedResults.Ok(snapshot)
                    : TypedResults.NoContent())
            .WithName("GetLatestCoordinate");

        group.MapGet("/latest/image", Results<FileContentHttpResult, NoContent> (ICoordinateStore store) =>
                store.LatestImage is { } png
                    ? TypedResults.File(png, "image/png")
                    : TypedResults.NoContent())
            .WithName("GetLatestImage");

        group.MapPost("/extract", async Task<Results<Ok<CoordinateExtractionResult>, BadRequest<ApiError>, UnprocessableEntity<CoordinateExtractionResult>>> (
                IFormFile image,
                ICoordinateExtractor extractor,
                IOptions<CoordinateExtractionOptions> options,
                CancellationToken cancellationToken) => {
                    if (image.Length == 0)
                        return TypedResults.BadRequest(new ApiError("No image was uploaded."));

                    if (image.Length > options.Value.MaxUploadBytes)
                        return TypedResults.BadRequest(new ApiError("Uploaded image exceeds the maximum allowed size."));

                    using var memory = new MemoryStream();
                    await image.CopyToAsync(memory, cancellationToken);

                    var result = extractor.Extract(memory.ToArray());

                    return result.Success
                        ? TypedResults.Ok(result)
                        : TypedResults.UnprocessableEntity(result);
                })
            .DisableAntiforgery()
            .WithName("ExtractCoordinates");
    }
}
