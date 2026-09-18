using ViaPost.Models;

namespace ViaPost.Resources;

public sealed class DomainsResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<DomainList> ListAsync(CancellationToken cancellationToken = default) => Client.RequestAsync<DomainList>(HttpMethod.Get, "/v1/domains", cancellationToken: cancellationToken);
    public Task<CreateDomainResponse> CreateAsync(CreateDomainRequest request, CancellationToken cancellationToken = default) => Client.RequestAsync<CreateDomainResponse>(HttpMethod.Post, "/v1/domains", request, cancellationToken: cancellationToken);
    public Task<Domain> GetAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<Domain>(HttpMethod.Get, $"/v1/domains/{Id(id)}", cancellationToken: cancellationToken);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/domains/{Id(id)}", cancellationToken: cancellationToken);
    public Task<RotateDkimResponse> RotateDkimAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<RotateDkimResponse>(HttpMethod.Post, $"/v1/domains/{Id(id)}/dkim/rotate", cancellationToken: cancellationToken);
    public Task<DnsRecordList> GetDnsAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<DnsRecordList>(HttpMethod.Get, $"/v1/domains/{Id(id)}/dns", cancellationToken: cancellationToken);
    public Task<DomainHealth> GetHealthAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<DomainHealth>(HttpMethod.Get, $"/v1/domains/{Id(id)}/health", cancellationToken: cancellationToken);
    public Task<InboundDomainConfiguration> GetInboundAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<InboundDomainConfiguration>(HttpMethod.Get, $"/v1/domains/{Id(id)}/inbound", cancellationToken: cancellationToken);
    public Task<Domain> VerifyAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<Domain>(HttpMethod.Post, $"/v1/domains/{Id(id)}/verify", cancellationToken: cancellationToken);
}

public sealed class UsageResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<MonthlyUsage> GetAsync(CancellationToken cancellationToken = default) => Client.RequestAsync<MonthlyUsage>(HttpMethod.Get, "/v1/usage", cancellationToken: cancellationToken);
}
