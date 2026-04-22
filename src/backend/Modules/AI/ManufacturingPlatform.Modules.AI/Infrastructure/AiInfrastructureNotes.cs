namespace ManufacturingPlatform.Modules.AI.Infrastructure;

public static class AiInfrastructureNotes
{
    public static readonly string[] All =
    [
        "AI remains read-only against operational data and reads only approved reporting datasets",
        "Adapters must support either local models or API-compatible inference",
        "Prompt, input scope, and result logging are required for explainability and auditability"
    ];
}
