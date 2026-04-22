using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Modules.Platform.Application;
using ManufacturingPlatform.Modules.Platform.Contracts;
using ManufacturingPlatform.Modules.Platform.Domain;
using ManufacturingPlatform.Modules.Platform.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.Modules.Platform;

public sealed class PlatformModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "platform",
        "Platform",
        "platform",
        "Shared administration, identity, logging, and configuration services.",
        "Phase 1",
        PlatformCapabilities.All);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/platform").WithTags("Platform");

        group.MapGet("/overview", Results);
        group.MapPost("/session/login", (LoginRequest request, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.Login(request)));
        group.MapGet("/session", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetSession(httpContext.GetSessionToken())));
        group.MapGet("/users", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetUsers(httpContext.GetSessionToken())));
        group.MapGet("/audit-logs", (HttpContext httpContext, IManufacturingRuntime runtime, int? take) => RuntimeApi.Execute(() => runtime.GetAuditLogs(httpContext.GetSessionToken(), take ?? 20)));
    }

    private static Ok<PlatformModuleOverview> Results()
    {
        return TypedResults.Ok(
            new PlatformModuleOverview(
                "Identity, access, configuration, and audit foundations.",
                [nameof(User), nameof(Role), nameof(Permission), nameof(Department), nameof(AuditLog), nameof(Attachment), nameof(Notification)],
                PlatformCapabilities.All,
                PlatformInfrastructureNotes.All));
    }
}
