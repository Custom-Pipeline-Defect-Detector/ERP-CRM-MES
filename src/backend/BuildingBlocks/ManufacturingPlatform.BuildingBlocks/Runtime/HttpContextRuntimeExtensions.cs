using Microsoft.AspNetCore.Http;

namespace ManufacturingPlatform.BuildingBlocks.Runtime;

public static class HttpContextRuntimeExtensions
{
    public static string? GetSessionToken(this HttpContext httpContext)
    {
        return httpContext.Request.Headers["X-Session-Token"].FirstOrDefault();
    }
}
