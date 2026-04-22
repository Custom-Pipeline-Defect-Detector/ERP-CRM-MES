namespace ManufacturingPlatform.Modules.Platform.Domain;

public sealed record User(Guid Id, string UserName, string DisplayName);

public sealed record Role(Guid Id, string Code, string Name);

public sealed record Permission(Guid Id, string Code, string Name);

public sealed record Department(Guid Id, string Code, string Name);

public sealed record AuditLog(Guid Id, string EntityName, string ActionName, DateTimeOffset OccurredAt);

public sealed record Attachment(Guid Id, string FileName, string StoragePath);

public sealed record Notification(Guid Id, string Title, string Channel);
