using ViaPost.Models;

namespace ViaPost.Resources;

public sealed class SendResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<SendResult> SendAsync(SendEmailRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        return Client.RequestAsync<SendResult>(HttpMethod.Post, "/v1/send", request, idempotencyKey, cancellationToken);
    }

    private static void Validate(SendEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.From)) throw new ArgumentException("From cannot be empty.", nameof(request));
        if (request.To.Count is < 1 or > 50) throw new ArgumentException("To must contain 1 to 50 recipients.", nameof(request));
        if (request.Cc?.Count > 50) throw new ArgumentException("Cc cannot contain more than 50 recipients.", nameof(request));
        if (request.Bcc?.Count > 50) throw new ArgumentException("Bcc cannot contain more than 50 recipients.", nameof(request));
        if (request.Variables?.Count > 100) throw new ArgumentException("Variables cannot contain more than 100 entries.", nameof(request));
        if (request.Attachments?.Count > 10) throw new ArgumentException("Attachments cannot contain more than 10 entries.", nameof(request));
        if (request.Attachments?.Any(item => string.IsNullOrWhiteSpace(item.Filename)) is true) throw new ArgumentException("Attachment filename cannot be empty.", nameof(request));
        if (request.Stream is not ("transactional" or "marketing")) throw new ArgumentException("Stream must be transactional or marketing.", nameof(request));
    }
}

public sealed class MessagesResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<MessageList> ListAsync(MessageListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        if (options.Limit is <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.Period is not (null or "24h" or "7d" or "14d" or "30d")) throw new ArgumentException("Period must be 24h, 7d, 14d or 30d.", nameof(options));
        return Client.RequestAsync<MessageList>(HttpMethod.Get, BuildQuery("/v1/messages", ("cursor", options.Cursor), ("limit", options.Limit), ("status", options.Status), ("search", options.Search), ("period", options.Period), ("api_key_id", options.ApiKeyId)), cancellationToken: cancellationToken);
    }

    public Task<Message> GetAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<Message>(HttpMethod.Get, $"/v1/messages/{Id(id)}", cancellationToken: cancellationToken);
    public Task<MessageEventList> ListEventsAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<MessageEventList>(HttpMethod.Get, $"/v1/messages/{Id(id)}/events", cancellationToken: cancellationToken);
    public Task<EngagementResponse> GetEngagementAsync(int? days = null, CancellationToken cancellationToken = default) => Client.RequestAsync<EngagementResponse>(HttpMethod.Get, BuildDays("/v1/messages/engagement", days), cancellationToken: cancellationToken);
    public Task<TimeseriesResponse> GetTimeseriesAsync(int? days = null, CancellationToken cancellationToken = default) => Client.RequestAsync<TimeseriesResponse>(HttpMethod.Get, BuildDays("/v1/messages/timeseries", days), cancellationToken: cancellationToken);
    public Task<MetricsResponse> GetMetricsAsync(int? days = null, Guid? domainId = null, CancellationToken cancellationToken = default) => Client.RequestAsync<MetricsResponse>(HttpMethod.Get, BuildQuery(BuildDays("/v1/messages/metrics", days), ("domain_id", domainId)), cancellationToken: cancellationToken);

    private static string BuildDays(string path, int? days)
    {
        if (days is < 1 or > 90) throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 1 and 90.");
        return BuildQuery(path, ("days", days));
    }
}
