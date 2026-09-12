using ViaPost.Models;

namespace ViaPost.Resources;

public sealed class AutomationsResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<AutomationList> ListAsync(AutomationListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        if (options.Status is not (null or "disabled" or "enabled" or "archived")) throw new ArgumentException("Invalid automation status.", nameof(options));
        return Client.RequestAsync<AutomationList>(HttpMethod.Get, BuildQuery("/v1/automations", ("status", options.Status), ("search", options.Search)), cancellationToken: cancellationToken);
    }

    public Task<Automation> CreateAsync(CreateAutomationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateName(request.Name, nameof(request));
        return Client.RequestAsync<Automation>(HttpMethod.Post, "/v1/automations", request, cancellationToken: cancellationToken);
    }

    public Task<Automation> GetAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<Automation>(HttpMethod.Get, $"/v1/automations/{Id(id)}", cancellationToken: cancellationToken);

    public Task<Automation> RenameAsync(Guid id, RenameAutomationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateName(request.Name, nameof(request));
        return Client.RequestAsync<Automation>(HttpMethod.Patch, $"/v1/automations/{Id(id)}", request, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/automations/{Id(id)}", cancellationToken: cancellationToken);
    public Task<Automation> ActivateAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<Automation>(HttpMethod.Post, $"/v1/automations/{Id(id)}/activate", cancellationToken: cancellationToken);
    public Task<Automation> DisableAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<Automation>(HttpMethod.Post, $"/v1/automations/{Id(id)}/disable", cancellationToken: cancellationToken);
    public Task<Automation> UpdateDraftAsync(Guid id, UpdateAutomationDraftRequest request, CancellationToken cancellationToken = default) => Client.RequestAsync<Automation>(HttpMethod.Patch, $"/v1/automations/{Id(id)}/draft", request, cancellationToken: cancellationToken);
    public Task<Automation> DuplicateAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<Automation>(HttpMethod.Post, $"/v1/automations/{Id(id)}/duplicate", cancellationToken: cancellationToken);

    public Task<AutomationRunList> ListRunsAsync(Guid id, AutomationRunListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        if (options.Limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.Status is not (null or "running" or "completed" or "failed" or "cancelled")) throw new ArgumentException("Invalid run status.", nameof(options));
        return Client.RequestAsync<AutomationRunList>(HttpMethod.Get, BuildQuery($"/v1/automations/{Id(id)}/runs", ("cursor", options.Cursor), ("limit", options.Limit), ("status", options.Status)), cancellationToken: cancellationToken);
    }

    public Task<AutomationRunDetail> GetRunAsync(Guid id, Guid runId, CancellationToken cancellationToken = default) => Client.RequestAsync<AutomationRunDetail>(HttpMethod.Get, $"/v1/automations/{Id(id)}/runs/{Id(runId)}", cancellationToken: cancellationToken);
    public Task CancelRunAsync(Guid id, Guid runId, CancellationToken cancellationToken = default) => Client.RequestNoContentAsync(HttpMethod.Post, $"/v1/automations/{Id(id)}/runs/{Id(runId)}/cancel", cancellationToken: cancellationToken);

    private static void ValidateName(string name, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Automation name cannot be empty.", parameterName);
    }
}
