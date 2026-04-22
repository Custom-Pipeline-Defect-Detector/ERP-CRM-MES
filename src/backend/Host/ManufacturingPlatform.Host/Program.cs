using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.Modules.AI;
using ManufacturingPlatform.Modules.CRM;
using ManufacturingPlatform.Modules.ERP;
using ManufacturingPlatform.Modules.Integration;
using ManufacturingPlatform.Modules.MES;
using ManufacturingPlatform.Modules.Platform;
using ManufacturingPlatform.Modules.Quality;
using ManufacturingPlatform.Modules.Reporting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

IModule[] modules =
[
    new PlatformModule(),
    new CrmModule(),
    new ErpModule(),
    new MesModule(),
    new QualityModule(),
    new ReportingModule(),
    new AiModule(),
    new IntegrationModule()
];

foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.MapGet("/", () => TypedResults.Ok(new
{
    Name = "ManufacturingPlatform.Host",
    Description = "On-premise manufacturing platform scaffold for CRM, ERP, MES, Quality, Reporting, AI, and Integration.",
    Modules = modules.Select(module => module.Descriptor.Key)
}));

app.MapGet("/api/health", () => TypedResults.Ok(new
{
    Status = "Healthy",
    CheckedAt = DateTimeOffset.UtcNow
}));

app.MapGet("/api/modules", () => TypedResults.Ok(modules.Select(module => module.Descriptor)));

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();
