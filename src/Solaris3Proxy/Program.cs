using Scalar.AspNetCore;
using Shiron.Solaris3Proxy.Endpoints;
using Shiron.Solaris3Proxy.Infrastructure;
using Shiron.Solaris3Proxy.Options;
using Shiron.Solaris3Proxy.Services;
using Shiron.Solaris3Proxy.Services.Impl;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CalculationOptions>(
    builder.Configuration.GetSection(CalculationOptions.SectionName));
builder.Services.Configure<CoordinateExtractionOptions>(
    builder.Configuration.GetSection(CoordinateExtractionOptions.SectionName));

builder.Services.AddSingleton<ICalculationStore, CalculationStore>();
builder.Services.AddHostedService<CalculationWorker>();
builder.Services.AddSingleton<ICoordinateExtractor, CoordinateExtractor>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Bridge system-installed Tesseract/Leptonica libraries to the names the wrapper expects.
TesseractNativeLibrary.EnsureAvailable(app.Logger);

if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.MapScalarApiReference(options => {
        options.Title = "Solaris3Proxy";
        options.Theme = ScalarTheme.Purple;
    });
}

app.MapCalculationEndpoints();
app.MapCoordinateEndpoints();

app.Run();
