using System.Text.Json;

namespace ViaPost.Models;

public sealed record Automation : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public JsonElement Graph { get; init; }
    public Guid? CurrentVersionId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record AutomationList
{
    public IReadOnlyList<Automation> Data { get; init; } = [];
}

public sealed record CreateAutomationRequest(string Name);
public sealed record RenameAutomationRequest(string Name);
public sealed record UpdateAutomationDraftRequest(JsonElement Graph) { public string? Name { get; init; } }
public sealed record AutomationListOptions(string? Status = null, string? Search = null);
public sealed record AutomationRunListOptions(string? Cursor = null, int? Limit = null, string? Status = null);

public sealed record AutomationRun : ExtensibleModel
{
    public Guid Id { get; init; }
    public Guid AutomationId { get; init; }
    public Guid AutomationVersionId { get; init; }
    public Guid ContactId { get; init; }
    public Guid TriggerEventId { get; init; }
    public string Status { get; init; } = string.Empty;
    public JsonElement GraphSnapshot { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record AutomationRunList
{
    public IReadOnlyList<AutomationRun> Data { get; init; } = [];
    public string? NextCursor { get; init; }
}

public sealed record AutomationRunStep : ExtensibleModel
{
    public Guid Id { get; init; }
    public string StepKey { get; init; } = string.Empty;
    public string StepType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Attempts { get; init; }
    public string? Branch { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record AutomationRunDetail
{
    public AutomationRun Run { get; init; } = new();
    public IReadOnlyList<AutomationRunStep> Steps { get; init; } = [];
}
