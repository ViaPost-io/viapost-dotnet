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
    public DateTimeOffset CreatedAt { get; init; }
}
public sealed record WebhookList { public IReadOnlyList<WebhookEndpoint> Webhooks { get; init; } = []; }
public sealed record CreateWebhookRequest(Uri Url, IReadOnlyList<string> EventTypes);
public sealed record CreateWebhookResponse
{
    public WebhookEndpoint Endpoint { get; init; } = new();
    public string Secret { get; init; } = string.Empty;

    public override string ToString() => $"{nameof(CreateWebhookResponse)} {{ Endpoint = {Endpoint}, Secret = [REDACTED] }}";
}
