using System.Text.Json;
using System.Text.Json.Serialization;

namespace ViaPost.Models;

[JsonConverter(typeof(OptionalValueJsonConverterFactory))]
public readonly struct OptionalValue<T>
{
    internal OptionalValue(T value)
    {
        HasValue = true;
        Value = value;
    }

    public bool HasValue { get; }
    public T? Value { get; }

}

public static class OptionalValue
{
    public static OptionalValue<T> From<T>(T value) => new(value);
}

public sealed class OptionalValueJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(OptionalValue<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(OptionalValueJsonConverter<>).MakeGenericType(valueType))!;
    }

    private sealed class OptionalValueJsonConverter<TValue> : JsonConverter<OptionalValue<TValue>>
    {
        public override bool HandleNull => true;

        public override OptionalValue<TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            OptionalValue.From(JsonSerializer.Deserialize<TValue>(ref reader, options)!);

        public override void Write(Utf8JsonWriter writer, OptionalValue<TValue> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Value, options);
    }
}

public abstract record ExtensibleModel
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record SendEmailRequest(string From, IReadOnlyList<string> To)
{
    public string? FromName { get; init; }
    public string? ReplyTo { get; init; }
    public IReadOnlyList<string>? Cc { get; init; }
    public IReadOnlyList<string>? Bcc { get; init; }
    public string? Subject { get; init; }
    public string? Html { get; init; }
    public string? Text { get; init; }
    public string Stream { get; init; } = "transactional";
    public IReadOnlyList<string>? Tags { get; init; }
    public IDictionary<string, object?>? Metadata { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public Guid? TemplateId { get; init; }
    public IDictionary<string, object?>? Variables { get; init; }
    public IReadOnlyList<Attachment>? Attachments { get; init; }
}

public sealed record Attachment(string Filename, string Content)
{
    public string? ContentType { get; init; }
}

public sealed record SendResult : ExtensibleModel
{
    public IReadOnlyList<AcceptedMessage>? Accepted { get; init; }
    public IReadOnlyList<RejectedMessage>? Rejected { get; init; }
}

public sealed record AcceptedMessage(Guid MessageId, string To);
public sealed record RejectedMessage(string To, string Reason);

public sealed record BatchSendMessage(string IdempotencyKey, SendEmailRequest Request);
public sealed record BatchSendRequest(IReadOnlyList<BatchSendMessage> Messages);
public sealed record BatchSendError(string Code, string Message);
public sealed record BatchSendResultItem
{
    public int Index { get; init; }
    public IReadOnlyList<AcceptedMessage>? Accepted { get; init; }
    public IReadOnlyList<RejectedMessage>? Rejected { get; init; }
    public BatchSendError? Error { get; init; }
}
public sealed record BatchSendResult { public IReadOnlyList<BatchSendResultItem> Results { get; init; } = []; }

public record Message : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Stream { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string ToAddress { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public string RecipientDomain { get; init; } = string.Empty;
    public Guid? ApiKeyId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public DateTimeOffset? QueuedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public DateTimeOffset? FailedAt { get; init; }
    public DateTimeOffset? SuppressedAt { get; init; }
    public DateTimeOffset? FirstOpenedAt { get; init; }
    public DateTimeOffset? FirstClickedAt { get; init; }
    public string? LastError { get; init; }
}

public sealed record MessageDetail : Message
{
    public string? BodyHtml { get; init; }
    public string? BodyPlain { get; init; }
    public string? ContentStatus { get; init; }
    public string? RawMessageApiPath { get; init; }
    public string? ContentVariant { get; init; }

    public override string ToString() => $"{nameof(MessageDetail)} {{ Id = {Id}, Status = {Status}, BodyHtml = [REDACTED], BodyPlain = [REDACTED], ContentStatus = {ContentStatus} }}";
}

public sealed record MessageList
{
    public IReadOnlyList<Message> Messages { get; init; } = [];
}

public sealed record MessageEvent : ExtensibleModel
{
    public string Type { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
    public string? Recipient { get; init; }
    public int? SmtpCode { get; init; }
    public string? EnhancedCode { get; init; }
    public string? Diagnostic { get; init; }
    public string? MxHost { get; init; }
    public Uri? ClickUrl { get; init; }
}

public sealed record MessageEventList
{
    public IReadOnlyList<MessageEvent> Events { get; init; } = [];
}

public sealed record MessageTimelineEvent : ExtensibleModel
{
    public Guid Id { get; init; }
    public Guid MessageId { get; init; }
    public string Type { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
    public string? Recipient { get; init; }
    public int? SmtpCode { get; init; }
    public string? EnhancedCode { get; init; }
    public string? Diagnostic { get; init; }
    public string? MxHost { get; init; }
    public Uri? ClickUrl { get; init; }

    public override string ToString() => $"{nameof(MessageTimelineEvent)} {{ Id = {Id}, MessageId = {MessageId}, Type = {Type}, Recipient = [REDACTED], Diagnostic = [REDACTED] }}";
}

public sealed record MessageTimelinePage
{
    public IReadOnlyList<MessageTimelineEvent> Data { get; init; } = [];
    public string? NextCursor { get; init; }
}

public sealed record MessageTimelineOptions(string? Cursor = null, int? Limit = null, string? Period = null, string? Type = null, Guid? MessageId = null);

public sealed record EngagementResponse(DateTimeOffset Since, long Delivered, long Opened, long Clicked);

public sealed record TimeseriesResponse : ExtensibleModel
{
    public DateTimeOffset Since { get; init; }
    public IReadOnlyList<JsonElement> Days { get; init; } = [];
}

public sealed record MetricsResponse : ExtensibleModel
{
    public DateTimeOffset Since { get; init; }
    public DateTimeOffset Until { get; init; }
    public JsonElement Current { get; init; }
    public JsonElement Previous { get; init; }
    public IReadOnlyList<JsonElement> Timeseries { get; init; } = [];
    public IReadOnlyList<JsonElement> ByDomain { get; init; } = [];
}

public sealed record MessageListOptions(DateTimeOffset? Cursor = null, int? Limit = null, string? Status = null, string? Search = null, string? Period = null, Guid? ApiKeyId = null);

public sealed record Domain : ExtensibleModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool SpfVerified { get; init; }
    public bool DkimVerified { get; init; }
    public bool DmarcVerified { get; init; }
    public string ReturnPathSubdomain { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record DomainList
{
    public IReadOnlyList<Domain> Domains { get; init; } = [];
}

public sealed record CreateDomainRequest(string Name);

public sealed record CreateDomainResponse : ExtensibleModel
{
    public Domain Domain { get; init; } = new();
    public IReadOnlyList<DnsRecord> DnsRecords { get; init; } = [];
}

public sealed record DnsRecord : ExtensibleModel
{
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
}

public sealed record DnsRecordList
{
    public IReadOnlyList<DnsRecord> DnsRecords { get; init; } = [];
}

public sealed record RotateDkimResponse : ExtensibleModel
{
    public string Selector { get; init; } = string.Empty;
    public string PublicKey { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed record InboundMXConfiguration(string Host, int Priority, string Status);
public sealed record InboundDomainConfiguration(string RecipientDomain, string Status, InboundMXConfiguration Mx);

public sealed record DomainHealthWindow(DateTimeOffset Start, DateTimeOffset End, int Days);
public sealed record DomainHealthCheck(bool Verified, string Status, int? Points, int MaxPoints);
public sealed record DomainHealthRateCheck(long Numerator, long Denominator, int? RateBasisPoints, string Status, int? Points, int MaxPoints);
public sealed record DomainHealthChecks(DomainHealthCheck Spf, DomainHealthCheck Dkim, DomainHealthCheck Dmarc, DomainHealthRateCheck DeliveryRate, DomainHealthRateCheck BounceRate);
public sealed record DomainHealthRecommendation(string Code, string Check, string Severity, string Message);
public sealed record DomainHealth
{
    public Guid DomainId { get; init; }
    public string DomainName { get; init; } = string.Empty;
    public string DomainStatus { get; init; } = string.Empty;
    public int? Score { get; init; }
    public string Status { get; init; } = string.Empty;
    public string CalculationVersion { get; init; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; init; }
    public DateTimeOffset? DnsCheckedAt { get; init; }
    public DomainHealthWindow Window { get; init; } = new(default, default, 30);
    public int MinimumSampleSize { get; init; }
    public long SampleSize { get; init; }
    public DomainHealthChecks Checks { get; init; } = new(new(false, string.Empty, null, 10), new(false, string.Empty, null, 20), new(false, string.Empty, null, 15), new(0, 0, null, string.Empty, null, 35), new(0, 0, null, string.Empty, null, 20));
    public IReadOnlyList<DomainHealthRecommendation> Recommendations { get; init; } = [];
}

public sealed record MonthlyUsage : ExtensibleModel
{
    public UsagePeriod Period { get; init; } = new();
    public long Used { get; init; }
    public long? Limit { get; init; }
    public long? Remaining { get; init; }
    public bool Unlimited { get; init; }
}

public sealed record UsagePeriod
{
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public string Timezone { get; init; } = "UTC";
}
