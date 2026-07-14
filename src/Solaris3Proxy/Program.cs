using Scalar.AspNetCore;
using Shiron.Solaris3Proxy.Endpoints;
using Shiron.Solaris3Proxy.Infrastructure;
using Shiron.Solaris3Proxy.Options;
using Shiron.Solaris3Proxy.Services;
using Shiron.Solaris3Proxy.Services.Impl;
using Shiron.Solaris3Proxy.Services.Screen;
using Shiron.Solaris3Proxy.Services.Screen.Impl;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CoordinateExtractionOptions>(
    builder.Configuration.GetSection(CoordinateExtractionOptions.SectionName));
builder.Services.Configure<ScreenCaptureOptions>(
    builder.Configuration.GetSection(ScreenCaptureOptions.SectionName));

builder.Services.AddSingleton<ICoordinateExtractor, CoordinateExtractor>();
builder.Services.AddSingleton<ICoordinateStore, CoordinateStore>();
builder.Services.AddSingleton<IScreenCapturerFactory, ScreenCapturerFactory>();
builder.Services.AddSingleton<IScreenCapturer>(sp => sp.GetRequiredService<IScreenCapturerFactory>().Create());
builder.Services.AddHostedService<CoordinateCaptureWorker>();

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

app.MapCoordinateEndpoints();

app.Run();
