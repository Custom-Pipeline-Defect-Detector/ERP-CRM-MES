using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.BuildingBlocks.Abstractions;

public interface IModule
{
    ModuleDescriptor Descriptor { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
