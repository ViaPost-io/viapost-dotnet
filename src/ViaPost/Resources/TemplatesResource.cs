using ViaPost.Models;

namespace ViaPost.Resources;

public sealed class TemplatesResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<TemplateList> ListAsync(TemplateListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        if (options.Limit is <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        return Client.RequestAsync<TemplateList>(HttpMethod.Get, BuildQuery("/v1/templates", ("cursor", options.Cursor), ("limit", options.Limit), ("search", options.Search)), cancellationToken: cancellationToken);
    }

    public Task<CreateTemplateResponse> CreateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 256) throw new ArgumentException("Template name must contain 1 to 256 characters.", nameof(request));
        return Client.RequestAsync<CreateTemplateResponse>(HttpMethod.Post, "/v1/templates", request, cancellationToken: cancellationToken);
    }

    public Task<EmailTemplate> GetAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<EmailTemplate>(HttpMethod.Get, $"/v1/templates/{Id(id)}", cancellationToken: cancellationToken);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/templates/{Id(id)}", cancellationToken: cancellationToken);
    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestNoContentAsync(HttpMethod.Post, $"/v1/templates/{Id(id)}/archive", cancellationToken: cancellationToken);
    public Task<TemplateAssetPolicy> CreateAssetAsync(Guid id, CreateTemplateAssetRequest request, CancellationToken cancellationToken = default) => Client.RequestAsync<TemplateAssetPolicy>(HttpMethod.Post, $"/v1/templates/{Id(id)}/assets", request, cancellationToken: cancellationToken);

    public Task<UpdateTemplateDraftResponse> UpdateDraftAsync(Guid id, UpdateTemplateDraftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Variables.Count > 100) throw new ArgumentException("Template variables cannot contain more than 100 entries.", nameof(request));
        ValidatePrecondition(request.ExpectedVersionId, request.ExpectedUpdatedAt, nameof(request));
        return Client.RequestAsync<UpdateTemplateDraftResponse>(HttpMethod.Patch, $"/v1/templates/{Id(id)}/draft", request, cancellationToken: cancellationToken);
    }

    public Task<CreateTemplateResponse> DuplicateAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<CreateTemplateResponse>(HttpMethod.Post, $"/v1/templates/{Id(id)}/duplicate", cancellationToken: cancellationToken);

    public Task<PreviewTemplateResponse> PreviewAsync(Guid id, PreviewTemplateRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new();
        if (request.Variables.Count > 100) throw new ArgumentException("Preview variables cannot contain more than 100 entries.", nameof(request));
        return Client.RequestAsync<PreviewTemplateResponse>(HttpMethod.Post, $"/v1/templates/{Id(id)}/preview", request, cancellationToken: cancellationToken);
    }

    public Task<EmailTemplateVersion> PublishAsync(Guid id, TemplatePreconditionRequest? request = null, CancellationToken cancellationToken = default)
    {
        if (request is not null) ValidatePrecondition(request.ExpectedVersionId, request.ExpectedUpdatedAt, nameof(request));
        return Client.RequestAsync<EmailTemplateVersion>(HttpMethod.Post, $"/v1/templates/{Id(id)}/publish", request, cancellationToken: cancellationToken);
    }

    public Task<TemplateVersionList> ListVersionsAsync(Guid id, CancellationToken cancellationToken = default) => Client.RequestAsync<TemplateVersionList>(HttpMethod.Get, $"/v1/templates/{Id(id)}/versions", cancellationToken: cancellationToken);
    public Task<EmailTemplateVersion> GetVersionAsync(Guid id, Guid versionId, CancellationToken cancellationToken = default) => Client.RequestAsync<EmailTemplateVersion>(HttpMethod.Get, $"/v1/templates/{Id(id)}/versions/{Id(versionId)}", cancellationToken: cancellationToken);

    public Task<EmailTemplateVersion> RevertAsync(Guid id, Guid versionId, TemplatePreconditionRequest? request = null, CancellationToken cancellationToken = default)
    {
        if (request is not null) ValidatePrecondition(request.ExpectedVersionId, request.ExpectedUpdatedAt, nameof(request));
        return Client.RequestAsync<EmailTemplateVersion>(HttpMethod.Post, $"/v1/templates/{Id(id)}/versions/{Id(versionId)}/revert", request, cancellationToken: cancellationToken);
    }

    private static void ValidatePrecondition(Guid? expectedVersionId, DateTimeOffset? expectedUpdatedAt, string parameterName)
    {
        if (expectedVersionId.HasValue != expectedUpdatedAt.HasValue)
            throw new ArgumentException("ExpectedVersionId and ExpectedUpdatedAt must be provided together.", parameterName);
    }
}
