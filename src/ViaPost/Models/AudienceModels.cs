using System.Text.Json;
using System.Text.Json.Serialization;

namespace ViaPost.Models;

public sealed record Contact : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public bool Subscribed { get; init; }
    public JsonElement Properties { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public override string ToString() => $"{nameof(Contact)} {{ Id = {Id}, Email = [REDACTED], Subscribed = {Subscribed} }}";
}

public sealed record ContactList
{
    public IReadOnlyList<Contact> Data { get; init; } = [];
    public string? NextCursor { get; init; }
}

public sealed record ContactListOptions(string? Cursor = null, int? Limit = null, string? Search = null);
public sealed record ContactImportResult(int Total, int Created, int Skipped, int Duplicates);

public sealed record CreateContactRequest(string Email)
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public bool? Subscribed { get; init; }
    public JsonElement? Properties { get; init; }

    public override string ToString() => $"{nameof(CreateContactRequest)} {{ Email = [REDACTED] }}";
}

public sealed record UpdateContactRequest
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalValue<string> Email { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalValue<string?> FirstName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalValue<string?> LastName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalValue<bool> Subscribed { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalValue<JsonElement> Properties { get; init; }

    public override string ToString() => $"{nameof(UpdateContactRequest)} {{ Email = [REDACTED] }}";
}

public sealed record CustomEvent : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public JsonElement? Schema { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record CustomEventList
{
    public IReadOnlyList<CustomEvent> Data { get; init; } = [];
}

public sealed record CreateCustomEventRequest(string Name)
{
    public JsonElement? Schema { get; init; }
}

public sealed record UpdateCustomEventRequest(JsonElement Schema);

public sealed record SendCustomEventRequest(string Event, Guid ContactId)
{
    public JsonElement? Properties { get; init; }

    public override string ToString() => $"{nameof(SendCustomEventRequest)} {{ Event = {Event}, ContactId = {ContactId}, Properties = [REDACTED] }}";
}

public sealed record CustomEventDelivery(Guid Id, Guid ContactId, string Event, DateTimeOffset OccurredAt);

public record InboundMessage : ExtensibleModel
{
    public Guid Id { get; init; }
    public Guid DomainId { get; init; }
    public string FromAddress { get; init; } = string.Empty;
    public string ToAddress { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public int AttachmentCount { get; init; }
    public bool IsFeedbackReport { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }

    public override string ToString() => $"{nameof(InboundMessage)} {{ Id = {Id}, DomainId = {DomainId}, FromAddress = [REDACTED], ToAddress = [REDACTED], Subject = [REDACTED], AttachmentCount = {AttachmentCount}, ReceivedAt = {ReceivedAt:O} }}";
}

public sealed record InboundMessageList
{
    public IReadOnlyList<InboundMessage> Messages { get; init; } = [];
}

public sealed record InboundMessageListOptions(
    DateTimeOffset? Cursor = null,
    int? Limit = null,
    Guid? DomainId = null,
    string? Search = null,
    string? Period = null,
    bool? HasAttachments = null);

public sealed record InboundAttachment : ExtensibleModel
{
    public string Filename { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public Uri? DownloadUrl { get; init; }

    public override string ToString() => $"{nameof(InboundAttachment)} {{ Filename = [REDACTED], ContentType = {ContentType}, SizeBytes = {SizeBytes}, DownloadUrl = [REDACTED] }}";
}

public sealed record InboundMessageDetail : InboundMessage
{
    public string? BodyHtml { get; init; }
    public string? BodyPlain { get; init; }
    public string ContentStatus { get; init; } = string.Empty;
    public string ContentVariant { get; init; } = string.Empty;
    public string? RawMessageApiPath { get; init; }
    public Uri? RawMessageUrl { get; init; }
    public IReadOnlyList<InboundAttachment> Attachments { get; init; } = [];
    public string? SpfResult { get; init; }
    public bool? SpfAligned { get; init; }
    public string? DkimResult { get; init; }
    public bool? DkimAligned { get; init; }
    public string? DmarcResult { get; init; }
    public string? DmarcDisposition { get; init; }
    public DateTimeOffset? AuthenticationEvaluatedAt { get; init; }

    public override string ToString() => $"{nameof(InboundMessageDetail)} {{ Id = {Id}, BodyHtml = [REDACTED], BodyPlain = [REDACTED], RawMessageUrl = [REDACTED], ContentStatus = {ContentStatus} }}";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StaticSegment), "static")]
[JsonDerivedType(typeof(DynamicSegment), "dynamic")]
public abstract record Segment : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? ContactCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    [JsonIgnore]
    public abstract string Kind { get; }
}

public sealed record StaticSegment : Segment
{
    public override string Kind => "static";
    public JsonElement? Definition { get; init; }
}

public sealed record DynamicSegment : Segment
{
    public override string Kind => "dynamic";
    public JsonElement Definition { get; init; }
}

public sealed record SegmentList
{
    public IReadOnlyList<Segment> Data { get; init; } = [];
    public string? NextCursor { get; init; }
}

public sealed record SegmentListOptions(string? Cursor = null, int? Limit = null, string? Search = null);
public abstract record CreateSegmentRequest(string Name) { public string? Description { get; init; } }

public sealed record StaticSegmentCreateRequest(string Name) : CreateSegmentRequest(Name)
{
    public string Kind { get; init; } = "static";
}

public sealed record DynamicSegmentCreateRequest(string Name, JsonElement Definition) : CreateSegmentRequest(Name)
{
    public string Kind { get; init; } = "dynamic";
}

public sealed record UpdateSegmentRequest
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalValue<string> Name { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalValue<string?> Description { get; init; }
}

public sealed record SegmentContactRequest(Guid ContactId);
public sealed record SegmentPreviewRequest(JsonElement Definition) { public int? Limit { get; init; } }
public sealed record SegmentPreview
{
    public long ContactCount { get; init; }
    public IReadOnlyList<Contact> Data { get; init; } = [];
}

public sealed record Suppression : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string? Note { get; init; }
    public Guid? DomainId { get; init; }
    public Guid? SourceMessageId { get; init; }
    public int? SmtpCode { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }
    public string? ReleaseReason { get; init; }
    public long Version { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public override string ToString() => $"{nameof(Suppression)} {{ Id = {Id}, Email = [REDACTED], Reason = {Reason}, State = {State}, Version = {Version} }}";
}

public sealed record SuppressionList
{
    public IReadOnlyList<Suppression> Data { get; init; } = [];
    public string? NextCursor { get; init; }
}

public sealed record SuppressionListOptions(string? Cursor = null, int? Limit = null, string? Search = null, string? Reason = null, string? State = null, string? Origin = null);
public sealed record SuppressionExportOptions(string? Search = null, string? Reason = null, string? State = null, string? Origin = null);
public sealed record SuppressionHistoryOptions(string? Cursor = null, int? Limit = null);

public sealed record CreateSuppressionRequest(string Email, string Reason)
{
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? Note { get; init; }

    public override string ToString() => $"{nameof(CreateSuppressionRequest)} {{ Email = [REDACTED], Reason = {Reason} }}";
}

public sealed record ReleaseSuppressionRequest(long ExpectedVersion, bool Acknowledge, string Justification)
{
    public override string ToString() => $"{nameof(ReleaseSuppressionRequest)} {{ ExpectedVersion = {ExpectedVersion}, Acknowledge = {Acknowledge}, Justification = [REDACTED] }}";
}

public sealed record SuppressionHistoryEvent : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string ActorType { get; init; } = string.Empty;
    public Guid? ActorId { get; init; }
    public string? Justification { get; init; }
    public long SuppressionVersion { get; init; }
    public DateTimeOffset OccurredAt { get; init; }

    public override string ToString() => $"{nameof(SuppressionHistoryEvent)} {{ Id = {Id}, Action = {Action}, Origin = {Origin}, Reason = {Reason}, ActorType = {ActorType}, ActorId = {ActorId}, Justification = [REDACTED], SuppressionVersion = {SuppressionVersion}, OccurredAt = {OccurredAt:O} }}";
}

public sealed record SuppressionDetail
{
    public Suppression Suppression { get; init; } = new();
    public IReadOnlyList<SuppressionHistoryEvent> History { get; init; } = [];
    public string? NextHistoryCursor { get; init; }
}

public sealed record SuppressionImportResult(long Total, long Created, long Reactivated, long Skipped, long Duplicates);

public sealed record Theme : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public JsonElement Style { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record ThemeList { public IReadOnlyList<Theme> Themes { get; init; } = []; }
public sealed record CreateThemeRequest(string Name, JsonElement Style);
