using Scalar.AspNetCore;
using Shiron.Solaris3Proxy.Endpoints;
using Shiron.Solaris3Proxy.Options;
using Shiron.Solaris3Proxy.Services;
using Shiron.Solaris3Proxy.Services.Impl;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CalculationOptions>(
    builder.Configuration.GetSection(CalculationOptions.SectionName));

builder.Services.AddSingleton<ICalculationStore, CalculationStore>();
builder.Services.AddHostedService<CalculationWorker>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.MapScalarApiReference(options => {
        options.Title = "Solaris3Proxy";
        options.Theme = ScalarTheme.Purple;
    });
}

app.MapCalculationEndpoints();

app.Run();
