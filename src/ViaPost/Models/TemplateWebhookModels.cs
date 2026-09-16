using System.Text.Json;

namespace ViaPost.Models;

public sealed record EmailTemplate : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid? CurrentDraftVersionId { get; init; }
    public Guid? PublishedVersionId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record TemplateList { public IReadOnlyList<EmailTemplate> Templates { get; init; } = []; }
public sealed record TemplateListOptions(DateTimeOffset? Cursor = null, int? Limit = null, string? Search = null);
public sealed record CreateTemplateRequest(string Name);
public sealed record CreateTemplateResponse { public EmailTemplate Template { get; init; } = new(); public EmailTemplateVersion Draft { get; init; } = new(); }
public sealed record TemplateVariable(string Name, string VarType) { public string? FallbackValue { get; init; } }

public sealed record EmailTemplateVersion : ExtensibleModel
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public JsonElement ContentJson { get; init; }
    public string? CompiledHtml { get; init; }
    public string? CompiledText { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public IReadOnlyList<TemplateVariable> Variables { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record TemplatePreconditionRequest(Guid? ExpectedVersionId = null, DateTimeOffset? ExpectedUpdatedAt = null);
public sealed record UpdateTemplateDraftRequest(JsonElement ContentJson, IReadOnlyList<TemplateVariable> Variables)
{
    public string? Subject { get; init; }
    public Guid? ExpectedVersionId { get; init; }
    public DateTimeOffset? ExpectedUpdatedAt { get; init; }
}
public sealed record UpdateTemplateDraftResponse { public EmailTemplateVersion Draft { get; init; } = new(); public string PreviewHtml { get; init; } = string.Empty; public string PreviewText { get; init; } = string.Empty; }
public sealed record PreviewTemplateRequest { public IDictionary<string, object?> Variables { get; init; } = new Dictionary<string, object?>(); }
public sealed record PreviewTemplateResponse(string Subject, string Html, string Text);
public sealed record TemplateVersionList { public IReadOnlyList<EmailTemplateVersion> Versions { get; init; } = []; }
public sealed record CreateTemplateAssetRequest(string Filename, string ContentType);
public sealed record TemplateAssetPolicy(Uri UploadUrl, IDictionary<string, string> UploadFields, Uri AssetUrl)
{
    public override string ToString() => $"{nameof(TemplateAssetPolicy)} {{ UploadUrl = [REDACTED], UploadFields = [REDACTED], AssetUrl = {AssetUrl} }}";
}

public sealed record WebhookEndpoint : ExtensibleModel
{
    public Guid Id { get; init; }
    public Uri Url { get; init; } = new("https://localhost");
    public IReadOnlyList<string> EventTypes { get; init; } = [];
    public bool Enabled { get; init; }
    public int MaxAttempts { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTimeOffset? DisabledAt { get; init; }
    public DateTimeOffset? SecretRotatedAt { get; init; }
    public long Version { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public override string ToString() => $"{nameof(WebhookEndpoint)} {{ Id = {Id}, Url = [REDACTED], Enabled = {Enabled}, Version = {Version} }}";
}
public sealed record WebhookList { public IReadOnlyList<WebhookEndpoint> Webhooks { get; init; } = []; }
public sealed record CreateWebhookRequest(Uri Url, IReadOnlyList<string> EventTypes)
{
    public override string ToString() => $"{nameof(CreateWebhookRequest)} {{ Url = [REDACTED], EventTypes = [{string.Join(", ", EventTypes)}] }}";
}
public sealed record CreateWebhookResponse
{
    public WebhookEndpoint Endpoint { get; init; } = new();
    public string Secret { get; init; } = string.Empty;

    public override string ToString() => $"{nameof(CreateWebhookResponse)} {{ Endpoint = {Endpoint}, Secret = [REDACTED] }}";
}

public sealed record UpdateWebhookRequest(long ExpectedVersion)
{
    public bool? Enabled { get; init; }
    public IReadOnlyList<string>? EventTypes { get; init; }
    public int? MaxAttempts { get; init; }
}

public sealed record WebhookDeliveryListOptions(string? Cursor = null, int? Limit = null, string? Status = null, string? EventType = null);

public record WebhookDeliverySummary
{
    public Guid DeliveryId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? NextRetryAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public int? LastResponseCode { get; init; }
    public long? LastDurationMs { get; init; }
    public bool IsTest { get; init; }
    public Guid? ReplayOfDeliveryId { get; init; }
}

public sealed record WebhookDeliveryPage
{
    public IReadOnlyList<WebhookDeliverySummary> Data { get; init; } = [];
    public string? NextCursor { get; init; }
}

public sealed record WebhookDeliveryAttempt
{
    public int Attempt { get; init; }
    public string Status { get; init; } = string.Empty;
    public int? ResponseCode { get; init; }
    public long? DurationMs { get; init; }
    public DateTimeOffset AttemptedAt { get; init; }
    public DateTimeOffset? NextRetryAt { get; init; }
}

public sealed record WebhookPayloadRedacted
{
    public string? EventType { get; init; }
    public Guid? MessageId { get; init; }
    public Guid? InboundMessageId { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
    public bool? Test { get; init; }

    public override string ToString() => $"{nameof(WebhookPayloadRedacted)} {{ EventType = {EventType}, MessageId = {MessageId}, InboundMessageId = {InboundMessageId}, OccurredAt = {OccurredAt:O}, Test = {Test} }}";
}

public sealed record WebhookDeliveryDetail : WebhookDeliverySummary
{
    public WebhookPayloadRedacted PayloadRedacted { get; init; } = new();
    public IReadOnlyList<WebhookDeliveryAttempt> Attempts { get; init; } = [];
}

public record WebhookOperationAccepted
{
    public Guid DeliveryId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record WebhookTestAccepted : WebhookOperationAccepted
{
    public bool IsTest { get; init; }
}

public sealed record WebhookReplayAccepted : WebhookOperationAccepted
{
    public Guid SourceDeliveryId { get; init; }
}

public sealed record RotateWebhookSecretResponse
{
    public WebhookEndpoint Endpoint { get; init; } = new();
    public string? Secret { get; init; }
    public DateTimeOffset RotatedAt { get; init; }

    public override string ToString() => $"{nameof(RotateWebhookSecretResponse)} {{ Endpoint = {Endpoint}, Secret = [REDACTED], RotatedAt = {RotatedAt:O} }}";
}
