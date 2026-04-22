using Microsoft.AspNetCore.Http;

namespace ManufacturingPlatform.BuildingBlocks.Runtime;

public class RuntimeRequestException(string code, string message, int statusCode)
    : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class RuntimeUnauthorizedException(string message = "Authentication is required.")
    : RuntimeRequestException("unauthorized", message, StatusCodes.Status401Unauthorized);

public sealed class RuntimeForbiddenException(string message = "You do not have permission to perform this action.")
    : RuntimeRequestException("forbidden", message, StatusCodes.Status403Forbidden);

public sealed class RuntimeNotFoundException(string message)
    : RuntimeRequestException("not_found", message, StatusCodes.Status404NotFound);

public sealed class RuntimeValidationException(string message)
    : RuntimeRequestException("validation_error", message, StatusCodes.Status400BadRequest);

public sealed record RuntimeErrorDto(string Code, string Message);

public static class RuntimeApi
{
    public static IResult Execute<T>(Func<T> operation)
    {
        try
        {
            return Results.Ok(operation());
        }
        catch (RuntimeRequestException exception)
        {
            return Results.Json(new RuntimeErrorDto(exception.Code, exception.Message), statusCode: exception.StatusCode);
        }
    }
}
