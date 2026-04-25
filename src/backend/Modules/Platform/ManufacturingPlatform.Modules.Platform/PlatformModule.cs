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
        group.MapPost("/session/change-password", (HttpContext httpContext, ChangePasswordRequest request, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.ChangePassword(httpContext.GetSessionToken(), request)));
        group.MapGet("/password-policy", (IManufacturingRuntime runtime) => RuntimeApi.Execute(runtime.GetPasswordPolicy));
        group.MapGet("/users", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetUsers(httpContext.GetSessionToken())));
        group.MapPost("/users/{userId:guid}/department", (HttpContext httpContext, IManufacturingRuntime runtime, Guid userId, UpdateUserDepartmentRequest request) => RuntimeApi.Execute(() => runtime.UpdateUserDepartment(httpContext.GetSessionToken(), userId, request)));
        group.MapPost("/users/{userId:guid}/roles", (HttpContext httpContext, IManufacturingRuntime runtime, Guid userId, UpdateUserRolesRequest request) => RuntimeApi.Execute(() => runtime.UpdateUserRoles(httpContext.GetSessionToken(), userId, request)));
        group.MapPost("/users/{userId:guid}/reset-password", (HttpContext httpContext, IManufacturingRuntime runtime, Guid userId) => RuntimeApi.Execute(() => runtime.ResetUserPassword(httpContext.GetSessionToken(), userId)));
        group.MapPost("/users/{userId:guid}/unlock", (HttpContext httpContext, IManufacturingRuntime runtime, Guid userId) => RuntimeApi.Execute(() => runtime.UnlockUser(httpContext.GetSessionToken(), userId)));
        group.MapGet("/departments", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetDepartments(httpContext.GetSessionToken())));
        group.MapPost("/departments", (HttpContext httpContext, IManufacturingRuntime runtime, CreateDepartmentRequest request) => RuntimeApi.Execute(() => runtime.CreateDepartment(httpContext.GetSessionToken(), request)));
        group.MapGet("/roles", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetRoles(httpContext.GetSessionToken())));
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
