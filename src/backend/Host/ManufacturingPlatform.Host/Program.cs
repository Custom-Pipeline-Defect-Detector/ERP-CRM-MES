using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Host.Runtime;
using ManufacturingPlatform.Host.Persistence;
using ManufacturingPlatform.Modules.AI;
using ManufacturingPlatform.Modules.CRM;
using ManufacturingPlatform.Modules.ERP;
using ManufacturingPlatform.Modules.Integration;
using ManufacturingPlatform.Modules.MES;
using ManufacturingPlatform.Modules.Platform;
using ManufacturingPlatform.Modules.Quality;
using ManufacturingPlatform.Modules.Reporting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ManufacturingOptions>(builder.Configuration.GetSection("Manufacturing"));
var manufacturingOptions = builder.Configuration.GetSection("Manufacturing").Get<ManufacturingOptions>() ?? new ManufacturingOptions();
builder.Services.AddDbContext<ManufacturingDbContext>(options =>
{
    var provider = manufacturingOptions.Database.Provider.Trim();
    var connectionString = manufacturingOptions.Database.ConnectionString;

    if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
        return;
    }

    options.UseSqlite(connectionString);
});
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "frontend",
        policy => policy
            .WithOrigins("http://127.0.0.1:4173", "http://localhost:4173", "http://127.0.0.1:5173", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddHttpClient(nameof(ManufacturingAiSummaryService));
builder.Services.AddHttpClient(nameof(AccountingExportGatewayService));
builder.Services.AddScoped<ManufacturingAiSummaryService>();
builder.Services.AddScoped<AccountingExportGatewayService>();
builder.Services.AddScoped<IManufacturingRuntime, EfManufacturingRuntime>();

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

await ManufacturingDbInitializer.InitializeAsync(app.Services);

app.UseExceptionHandler();
app.UseCors("frontend");

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
