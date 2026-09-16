using System.Net;
using System.Net.Sockets;
using ViaPost.Models;

namespace ViaPost.Resources;

public sealed class WebhooksResource(ViaPostClient client) : ResourceBase(client)
{
    private static readonly HashSet<string> SubscribableEventTypes = new(StringComparer.Ordinal)
    {
        "queued", "sent", "delivered", "deferred", "soft_bounce", "hard_bounce",
        "complaint", "open", "click", "unsubscribe", "rejected", "failed", "inbound.received"
    };

    private static readonly HashSet<string> DeliveryStatuses = new(StringComparer.Ordinal)
    {
        "pending", "delivered", "failed"
    };

    public Task<WebhookList> ListAsync(CancellationToken cancellationToken = default) => Client.RequestAsync<WebhookList>(HttpMethod.Get, "/v1/webhooks", cancellationToken: cancellationToken);

    public Task<CreateWebhookResponse> CreateAsync(CreateWebhookRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUrl(request.Url, nameof(request));
        ValidateEventTypes(request.EventTypes, nameof(request));
        return Client.RequestAsync<CreateWebhookResponse>(HttpMethod.Post, "/v1/webhooks", request, cancellationToken: cancellationToken);
    }

    public Task<WebhookEndpoint> UpdateAsync(Guid id, UpdateWebhookRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedVersion < 1) throw new ArgumentOutOfRangeException(nameof(request), "ExpectedVersion must be greater than zero.");
        if (request.Enabled is null && request.EventTypes is null && request.MaxAttempts is null) throw new ArgumentException("At least one webhook change is required.", nameof(request));
        if (request.EventTypes is not null) ValidateEventTypes(request.EventTypes, nameof(request));
        if (request.MaxAttempts is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(request), "MaxAttempts must be between 1 and 20.");
        return Client.RequestAsync<WebhookEndpoint>(HttpMethod.Patch, $"/v1/webhooks/{Id(id)}", request, cancellationToken: cancellationToken);
    }

    public Task<WebhookDeliveryPage> ListDeliveriesAsync(Guid id, WebhookDeliveryListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        if (options.Cursor is { Length: 0 }) throw new ArgumentException("Cursor cannot be empty.", nameof(options));
        if (options.Limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(options), "Limit must be between 1 and 100.");
        if (options.Status is not null && !DeliveryStatuses.Contains(options.Status)) throw new ArgumentException("Unsupported delivery status.", nameof(options));
        if (options.EventType is not null && options.EventType != "webhook.test" && !SubscribableEventTypes.Contains(options.EventType)) throw new ArgumentException("Unsupported webhook event type.", nameof(options));
        return Client.RequestAsync<WebhookDeliveryPage>(HttpMethod.Get, BuildQuery($"/v1/webhooks/{Id(id)}/deliveries", ("cursor", options.Cursor), ("limit", options.Limit), ("status", options.Status), ("event_type", options.EventType)), cancellationToken: cancellationToken);
    }

    public Task<WebhookDeliveryDetail> GetDeliveryAsync(Guid id, Guid deliveryId, CancellationToken cancellationToken = default) =>
        Client.RequestAsync<WebhookDeliveryDetail>(HttpMethod.Get, $"/v1/webhooks/{Id(id)}/deliveries/{Id(deliveryId)}", cancellationToken: cancellationToken);

    public Task<WebhookTestAccepted> TestAsync(Guid id, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ValidateIdempotencyKey(idempotencyKey);
        return Client.RequestAsync<WebhookTestAccepted>(HttpMethod.Post, $"/v1/webhooks/{Id(id)}/test", new { }, idempotencyKey, cancellationToken);
    }

    public Task<WebhookReplayAccepted> ReplayAsync(Guid id, Guid deliveryId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ValidateIdempotencyKey(idempotencyKey);
        return Client.RequestAsync<WebhookReplayAccepted>(HttpMethod.Post, $"/v1/webhooks/{Id(id)}/deliveries/{Id(deliveryId)}/replay", new { }, idempotencyKey, cancellationToken);
    }

    public Task<RotateWebhookSecretResponse> RotateSecretAsync(Guid id, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ValidateIdempotencyKey(idempotencyKey);
        return Client.RequestAsync<RotateWebhookSecretResponse>(HttpMethod.Post, $"/v1/webhooks/{Id(id)}/secret/rotate", new { }, idempotencyKey, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/webhooks/{Id(id)}", cancellationToken: cancellationToken);

    private static void ValidateEventTypes(IReadOnlyList<string> eventTypes, string parameterName)
    {
        if (eventTypes.Count == 0) throw new ArgumentException("At least one event type is required.", parameterName);
        if (eventTypes.Any(eventType => !SubscribableEventTypes.Contains(eventType))) throw new ArgumentException("Unsupported webhook event type.", parameterName);
        if (eventTypes.Distinct(StringComparer.Ordinal).Count() != eventTypes.Count) throw new ArgumentException("Event types must be unique.", parameterName);
    }

    private static void ValidateUrl(Uri url, string parameterName)
    {
        if (!url.IsAbsoluteUri || !url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Fragment))
            throw new ArgumentException("Webhook URL must be an absolute HTTPS URL without credentials or a fragment.", parameterName);

        var host = url.IdnHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Webhook URL must use a publicly routable destination.", parameterName);
        if (IPAddress.TryParse(host, out var address) && !IsPublicAddress(address))
            throw new ArgumentException("Webhook URL must use a publicly routable destination.", parameterName);
    }

    private static void ValidateIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey) || idempotencyKey.Length > 255 || idempotencyKey.Any(character => character is < '!' or > '~'))
            throw new ArgumentException("Idempotency key must contain 1 to 255 visible ASCII characters.", nameof(idempotencyKey));
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] != 0
                && bytes[0] != 10
                && bytes[0] != 127
                && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                && !(bytes[0] == 169 && bytes[1] == 254)
                && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                && !(bytes[0] == 192 && bytes[1] == 168)
                && !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0)
                && !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2)
                && !(bytes[0] == 198 && bytes[1] is 18 or 19)
                && !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
                && !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)
                && bytes[0] < 224;
        }

        var segments = address.GetAddressBytes();
        var documentation = segments[0] == 0x20 && segments[1] == 0x01 && segments[2] == 0x0d && segments[3] == 0xb8;
        var ipv4Compatible = segments[..12].All(value => value == 0);
        return !address.Equals(IPAddress.IPv6Any)
            && !address.IsIPv6LinkLocal
            && !address.IsIPv6Multicast
            && !address.IsIPv6SiteLocal
            && !address.IsIPv6UniqueLocal
            && !ipv4Compatible
            && !documentation;
    }
}
