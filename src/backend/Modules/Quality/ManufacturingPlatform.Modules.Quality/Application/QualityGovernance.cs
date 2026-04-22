using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.Modules.Quality.Domain;

namespace ManufacturingPlatform.Modules.Quality.Application;

public static class QualityGovernance
{
    public static readonly DataOwnershipDefinition[] OwnershipRules =
    [
        new(nameof(InspectionPlan), "Quality", ["ERP", "MES", "Reporting"], "Quality owns inspection definition and control sampling logic."),
        new(nameof(InspectionRecord), "Quality", ["ERP", "MES", "Reporting"], "Quality owns the inspection result and disposition decision."),
        new(nameof(HoldReleaseRecord), "Quality", ["ERP", "Reporting"], "Stock release, hold, and reject decisions are governed by Quality."),
        new(nameof(Ncr), "Quality", ["MES", "ERP", "Reporting"], "Nonconformance ownership remains in Quality with cross-functional execution.")
    ];

    public static readonly WorkflowDefinition[] WorkflowDefinitions =
    [
        new(
            nameof(InspectionRecord),
            "Quality",
            Enum.GetNames<InspectionRecordState>(),
            [
                new(nameof(InspectionRecordState.Planned), nameof(InspectionRecordState.SampleCollected), "Collect sample", "Quality inspector"),
                new(nameof(InspectionRecordState.SampleCollected), nameof(InspectionRecordState.InInspection), "Start inspection", "Quality inspector"),
                new(nameof(InspectionRecordState.InInspection), nameof(InspectionRecordState.Accepted), "Accept lot", "Quality engineer"),
                new(nameof(InspectionRecordState.InInspection), nameof(InspectionRecordState.Rejected), "Reject lot", "Quality engineer"),
                new(nameof(InspectionRecordState.InInspection), nameof(InspectionRecordState.ConditionalRelease), "Conditional release", "Quality manager"),
                new(nameof(InspectionRecordState.Accepted), nameof(InspectionRecordState.Closed), "Close inspection", "Quality"),
                new(nameof(InspectionRecordState.Rejected), nameof(InspectionRecordState.Closed), "Close inspection", "Quality"),
                new(nameof(InspectionRecordState.ConditionalRelease), nameof(InspectionRecordState.Closed), "Close inspection", "Quality")
            ])
    ];
}
