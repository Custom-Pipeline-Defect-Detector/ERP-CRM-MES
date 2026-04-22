namespace ManufacturingPlatform.Modules.Reporting.Infrastructure;

public static class ReportingInfrastructureNotes
{
    public static readonly string[] All =
    [
        "Official reporting consumes approved semantic datasets rather than direct raw-table reads",
        "Heavy exports and scheduled reports belong in background jobs",
        "Department-level permissions and drill-down definitions apply to every dashboard and report"
    ];
}
