using ViaPost.Models;

namespace ViaPost.Resources;

public sealed class WebhooksResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<WebhookList> ListAsync(CancellationToken cancellationToken = default) => Client.RequestAsync<WebhookList>(HttpMethod.Get, "/v1/webhooks", cancellationToken: cancellationToken);

    public Task<CreateWebhookResponse> CreateAsync(CreateWebhookRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Url.IsAbsoluteUri || request.Url.Scheme is not ("http" or "https")) throw new ArgumentException("Webhook URL must use HTTP or HTTPS.", nameof(request));
        if (request.EventTypes.Count == 0) throw new ArgumentException("At least one event type is required.", nameof(request));
        return Client.RequestAsync<CreateWebhookResponse>(HttpMethod.Post, "/v1/webhooks", request, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/webhooks/{Id(id)}", cancellationToken: cancellationToken);
}
