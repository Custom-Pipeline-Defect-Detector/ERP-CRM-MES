namespace ManufacturingPlatform.Modules.CRM.Domain;

public enum CustomerStatus
{
    Prospect,
    Active,
    OnHold,
    Inactive,
    Blacklisted
}

public enum CustomerSegment
{
    Strategic,
    KeyAccount,
    Distributor,
    Standard,
    ServiceOnly
}

public enum LeadStatus
{
    Draft,
    Qualified,
    Disqualified,
    Converted,
    Archived
}

public enum OpportunityStage
{
    Qualification,
    SolutionFit,
    Quotation,
    Negotiation,
    Won,
    Lost
}

public enum QuoteState
{
    Draft,
    Submitted,
    Approved,
    Sent,
    Accepted,
    Rejected,
    Expired
}

public enum ContractState
{
    Draft,
    UnderReview,
    Approved,
    Effective,
    Suspended,
    Closed,
    Cancelled
}

public enum SalesOrderState
{
    Draft,
    Submitted,
    Approved,
    Released,
    PartiallyFulfilled,
    Completed,
    Closed,
    Cancelled
}

public enum SalesActivityType
{
    Call,
    Visit,
    Meeting,
    Message,
    Complaint,
    InternalReview
}

public enum FollowUpTaskState
{
    Open,
    InProgress,
    Done,
    Cancelled,
    Overdue
}

public enum ReminderState
{
    Scheduled,
    Triggered,
    Acknowledged,
    Closed
}

public enum AfterSalesTicketState
{
    Open,
    Assigned,
    InAnalysis,
    PendingCustomer,
    Resolved,
    Closed,
    Cancelled
}

public sealed record Customer(
    Guid Id,
    string Code,
    string Name,
    CustomerStatus Status,
    CustomerSegment Segment,
    string Region,
    string Industry,
    string CreditRating);

public sealed record CustomerContact(
    Guid Id,
    Guid CustomerId,
    string Name,
    string JobTitle,
    string Phone,
    string Email,
    bool IsPrimary);

public sealed record Lead(
    Guid Id,
    string LeadNumber,
    string CompanyName,
    LeadStatus Status,
    string Source,
    Guid? AssignedSalesUserId,
    DateOnly CreatedOn);

public sealed record LeadQualificationRecord(
    Guid Id,
    Guid LeadId,
    DateTimeOffset EvaluatedAt,
    bool IsQualified,
    string Summary);

public sealed record Opportunity(
    Guid Id,
    string OpportunityNumber,
    Guid CustomerId,
    OpportunityStage Stage,
    decimal EstimatedAmount,
    decimal ProbabilityPercent,
    DateOnly ExpectedCloseDate,
    string? CompetitorName,
    string? WinLossReason);

public sealed record OpportunityStageHistory(
    Guid Id,
    Guid OpportunityId,
    OpportunityStage Stage,
    DateTimeOffset ChangedAt,
    string Note);

public sealed record Quote(
    Guid Id,
    string QuoteNumber,
    Guid OpportunityId,
    int RevisionNumber,
    QuoteState State,
    decimal TotalAmount,
    string CurrencyCode,
    DateOnly QuotedOn);

public sealed record SalesActivity(
    Guid Id,
    Guid CustomerId,
    Guid? OpportunityId,
    SalesActivityType ActivityType,
    DateTimeOffset OccurredAt,
    string Summary,
    Guid OwnerUserId);

public sealed record FollowUpTask(
    Guid Id,
    Guid CustomerId,
    Guid? OpportunityId,
    string TaskTitle,
    FollowUpTaskState State,
    DateTimeOffset DueAt,
    Guid OwnerUserId);

public sealed record Reminder(
    Guid Id,
    Guid FollowUpTaskId,
    DateTimeOffset ReminderAt,
    ReminderState State,
    string Channel);

public sealed record Contract(
    Guid Id,
    string ContractNumber,
    Guid CustomerId,
    ContractState State,
    DateOnly EffectiveDate,
    DateOnly? ExpirationDate);

public sealed record ContractRevision(
    Guid Id,
    Guid ContractId,
    int RevisionNumber,
    ContractState State,
    DateTimeOffset RevisedAt);

public sealed record SalesOrder(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    Guid? ContractId,
    SalesOrderState State,
    DateOnly OrderDate,
    decimal TotalAmount);

public sealed record SalesTarget(
    Guid Id,
    Guid SalesOwnerUserId,
    string MetricName,
    decimal TargetValue,
    DateOnly PeriodStart,
    DateOnly PeriodEnd);

public sealed record SalesPerformanceSnapshot(
    Guid Id,
    Guid SalesOwnerUserId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal WonAmount,
    int WonOrderCount,
    decimal ConversionRatePercent);

public sealed record AfterSalesTicket(
    Guid Id,
    string TicketNumber,
    Guid CustomerId,
    Guid? SalesOrderId,
    AfterSalesTicketState State,
    string? ShippedLotNumber,
    string? FinishedGoodsBatchNumber,
    string ProblemSummary);

public sealed record WarrantyClaim(
    Guid Id,
    Guid AfterSalesTicketId,
    string ClaimNumber,
    DateOnly ClaimedOn,
    decimal ClaimedAmount);
