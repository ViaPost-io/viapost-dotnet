using System.Net.Mail;
using System.Text.Json;
using ViaPost.Models;

namespace ViaPost.Resources;

public sealed class ContactsResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<ContactList> ListAsync(ContactListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        ValidatePage(options.Cursor, options.Limit, 200, nameof(options));
        return Client.RequestAsync<ContactList>(HttpMethod.Get, BuildQuery("/v1/contacts", ("cursor", options.Cursor), ("limit", options.Limit), ("search", options.Search)), cancellationToken: cancellationToken);
    }

    public Task<Contact> CreateAsync(CreateContactRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEmail(request.Email, nameof(request));
        ValidateObject(request.Properties, nameof(request));
        return Client.RequestAsync<Contact>(HttpMethod.Post, "/v1/contacts", request, cancellationToken: cancellationToken);
    }

    public Task<Contact> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Client.RequestAsync<Contact>(HttpMethod.Get, $"/v1/contacts/{Id(id)}", cancellationToken: cancellationToken);

    public Task<Contact> UpdateAsync(Guid id, UpdateContactRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Email.HasValue && !request.FirstName.HasValue && !request.LastName.HasValue && !request.Subscribed.HasValue && !request.Properties.HasValue)
            throw new ArgumentException("At least one contact change is required.", nameof(request));
        if (request.Email.HasValue) ValidateEmail(request.Email.Value!, nameof(request));
        if (request.Properties.HasValue && request.Properties.Value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Contact properties must be a JSON object.", nameof(request));
        return Client.RequestAsync<Contact>(HttpMethod.Patch, $"/v1/contacts/{Id(id)}", request, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/contacts/{Id(id)}", cancellationToken: cancellationToken);

    public Task<ContactImportResult> ImportAsync(ReadOnlyMemory<byte> csv, CancellationToken cancellationToken = default)
    {
        if (csv.Length is < 1 or > 2 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(csv), "CSV must contain 1 to 2097152 bytes.");
        return Client.RequestContentAsync<ContactImportResult>(HttpMethod.Post, "/v1/contacts/import", csv.ToArray(), "text/csv", cancellationToken: cancellationToken);
    }

    internal static void ValidatePage(string? cursor, int? limit, int maximumLimit, string parameterName)
    {
        if (cursor is { Length: 0 }) throw new ArgumentException("Cursor cannot be empty.", parameterName);
        if (limit is < 1 || limit > maximumLimit) throw new ArgumentOutOfRangeException(parameterName, $"Limit must be between 1 and {maximumLimit}.");
    }

    internal static void ValidateEmail(string email, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length is < 3 or > 254 || !MailAddress.TryCreate(email, out var parsed) || !parsed.Address.Equals(email, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Email must be a valid address with at most 254 characters.", parameterName);
    }

    private static void ValidateObject(JsonElement? value, string parameterName)
    {
        if (value is { ValueKind: not JsonValueKind.Object }) throw new ArgumentException("Properties must be a JSON object.", parameterName);
    }
}

public sealed class EventsResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<CustomEventList> ListAsync(CancellationToken cancellationToken = default) =>
        Client.RequestAsync<CustomEventList>(HttpMethod.Get, "/v1/events", cancellationToken: cancellationToken);

    public Task<CustomEvent> CreateAsync(CreateCustomEventRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateName(request.Name, nameof(request));
        ValidateObject(request.Schema, nameof(request));
        return Client.RequestAsync<CustomEvent>(HttpMethod.Post, "/v1/events", request, cancellationToken: cancellationToken);
    }

    public Task<CustomEventDelivery> SendAsync(SendCustomEventRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateName(request.Event, nameof(request));
        ValidateObject(request.Properties, nameof(request));
        return Client.RequestAsync<CustomEventDelivery>(HttpMethod.Post, "/v1/events/send", request, cancellationToken: cancellationToken);
    }

    public Task<CustomEvent> UpdateAsync(Guid id, UpdateCustomEventRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Schema.ValueKind != JsonValueKind.Object) throw new ArgumentException("Event schema must be a JSON object.", nameof(request));
        return Client.RequestAsync<CustomEvent>(HttpMethod.Patch, $"/v1/events/{Id(id)}", request, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/events/{Id(id)}", cancellationToken: cancellationToken);

    private static void ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Event name cannot be empty.", parameterName);
    }

    private static void ValidateObject(JsonElement? value, string parameterName)
    {
        if (value is { ValueKind: not JsonValueKind.Object }) throw new ArgumentException("Value must be a JSON object.", parameterName);
    }
}

public sealed class InboundMessagesResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<InboundMessageList> ListAsync(InboundMessageListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        if (options.Limit is <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Limit must be greater than zero.");
        if (options.Period is not (null or "7d" or "30d")) throw new ArgumentException("Period must be 7d or 30d.", nameof(options));
        return Client.RequestAsync<InboundMessageList>(HttpMethod.Get, BuildQuery(
            "/v1/inbound-messages",
            ("cursor", options.Cursor),
            ("limit", options.Limit),
            ("domain_id", options.DomainId),
            ("search", options.Search),
            ("period", options.Period),
            ("has_attachments", options.HasAttachments)), cancellationToken: cancellationToken);
    }

    public Task<InboundMessageDetail> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Client.RequestAsync<InboundMessageDetail>(HttpMethod.Get, $"/v1/inbound-messages/{Id(id)}", cancellationToken: cancellationToken);

    public Task<byte[]> GetRawAsync(Guid id, CancellationToken cancellationToken = default) =>
        Client.RequestRawMessageAsync($"/v1/inbound-messages/{Id(id)}/raw", cancellationToken);
}

public sealed class SegmentsResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<SegmentList> ListAsync(SegmentListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        ContactsResource.ValidatePage(options.Cursor, options.Limit, 200, nameof(options));
        return Client.RequestAsync<SegmentList>(HttpMethod.Get, BuildQuery("/v1/segments", ("cursor", options.Cursor), ("limit", options.Limit), ("search", options.Search)), cancellationToken: cancellationToken);
    }

    public Task<Segment> CreateAsync(CreateSegmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateName(request.Name, nameof(request));
        return Client.RequestAsync<Segment>(HttpMethod.Post, "/v1/segments", request, cancellationToken: cancellationToken);
    }

    public Task<Segment> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Client.RequestAsync<Segment>(HttpMethod.Get, $"/v1/segments/{Id(id)}", cancellationToken: cancellationToken);

    public Task<Segment> UpdateAsync(Guid id, UpdateSegmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Name.HasValue && !request.Description.HasValue) throw new ArgumentException("At least one segment change is required.", nameof(request));
        if (request.Name.HasValue) ValidateName(request.Name.Value!, nameof(request));
        return Client.RequestAsync<Segment>(HttpMethod.Patch, $"/v1/segments/{Id(id)}", request, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/segments/{Id(id)}", cancellationToken: cancellationToken);

    public Task<ContactList> ListContactsAsync(Guid id, ContactListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        ContactsResource.ValidatePage(options.Cursor, options.Limit, 200, nameof(options));
        return Client.RequestAsync<ContactList>(HttpMethod.Get, BuildQuery($"/v1/segments/{Id(id)}/contacts", ("cursor", options.Cursor), ("limit", options.Limit), ("search", options.Search)), cancellationToken: cancellationToken);
    }

    public Task AddContactAsync(Guid id, SegmentContactRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Client.RequestNoContentAsync(HttpMethod.Post, $"/v1/segments/{Id(id)}/contacts", request, cancellationToken);
    }

    public Task RemoveContactAsync(Guid id, Guid contactId, CancellationToken cancellationToken = default) =>
        Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/segments/{Id(id)}/contacts/{Id(contactId)}", cancellationToken: cancellationToken);

    public Task<SegmentPreview> PreviewAsync(SegmentPreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Definition.ValueKind != JsonValueKind.Object) throw new ArgumentException("Segment definition must be a JSON object.", nameof(request));
        if (request.Limit is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(request), "Limit must be between 1 and 50.");
        return Client.RequestAsync<SegmentPreview>(HttpMethod.Post, "/v1/segments/preview", request, cancellationToken: cancellationToken);
    }

    private static void ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Segment name cannot be empty.", parameterName);
    }
}

public sealed class SuppressionsResource(ViaPostClient client) : ResourceBase(client)
{
    private const int MaximumImportBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> Reasons = new(StringComparer.Ordinal) { "hard_bounce", "complaint", "unsubscribe", "manual", "invalid_address", "spam_trap" };
    private static readonly HashSet<string> CreateReasons = new(StringComparer.Ordinal) { "manual", "invalid_address" };
    private static readonly HashSet<string> States = new(StringComparer.Ordinal) { "active", "expired", "released", "all" };
    private static readonly HashSet<string> Origins = new(StringComparer.Ordinal) { "manual", "import", "automatic" };

    public Task<SuppressionList> ListAsync(SuppressionListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        ContactsResource.ValidatePage(options.Cursor, options.Limit, 50, nameof(options));
        ValidateFilters(options.Search, options.Reason, options.State, options.Origin, nameof(options));
        return Client.RequestAsync<SuppressionList>(HttpMethod.Get, BuildQuery("/v1/suppressions", ("cursor", options.Cursor), ("limit", options.Limit), ("search", options.Search), ("reason", options.Reason), ("state", options.State), ("origin", options.Origin)), cancellationToken: cancellationToken);
    }

    public Task<Suppression> CreateAsync(CreateSuppressionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ContactsResource.ValidateEmail(request.Email, nameof(request));
        if (!CreateReasons.Contains(request.Reason)) throw new ArgumentException("Create reason must be manual or invalid_address.", nameof(request));
        if (request.Note?.Length > 500) throw new ArgumentException("Note cannot exceed 500 characters.", nameof(request));
        return Client.RequestAsync<Suppression>(HttpMethod.Post, "/v1/suppressions", request, cancellationToken: cancellationToken);
    }

    public Task<SuppressionImportResult> ImportAsync(ReadOnlyMemory<byte> csv, CancellationToken cancellationToken = default)
    {
        if (csv.Length is < 1 or > MaximumImportBytes) throw new ArgumentOutOfRangeException(nameof(csv), $"CSV must contain 1 to {MaximumImportBytes} bytes.");
        return Client.RequestContentAsync<SuppressionImportResult>(HttpMethod.Post, "/v1/suppressions/import", csv.ToArray(), "text/csv", cancellationToken: cancellationToken);
    }

    public Task<byte[]> ExportAsync(SuppressionExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        ValidateFilters(options.Search, options.Reason, options.State, options.Origin, nameof(options));
        return Client.RequestExportAsync(BuildQuery("/v1/suppressions/export", ("search", options.Search), ("reason", options.Reason), ("state", options.State), ("origin", options.Origin)), "text/csv", cancellationToken);
    }

    public Task<SuppressionDetail> GetAsync(Guid id, SuppressionHistoryOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        ContactsResource.ValidatePage(options.Cursor, options.Limit, 100, nameof(options));
        return Client.RequestAsync<SuppressionDetail>(HttpMethod.Get, BuildQuery($"/v1/suppressions/{Id(id)}", ("history_cursor", options.Cursor), ("history_limit", options.Limit)), cancellationToken: cancellationToken);
    }

    public Task<Suppression> ReleaseAsync(Guid id, ReleaseSuppressionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedVersion < 1) throw new ArgumentOutOfRangeException(nameof(request), "ExpectedVersion must be greater than zero.");
        if (!request.Acknowledge) throw new ArgumentException("Acknowledge must be true.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Length is < 10 or > 500)
            throw new ArgumentException("Justification must contain 10 to 500 characters.", nameof(request));
        return Client.RequestAsync<Suppression>(HttpMethod.Post, $"/v1/suppressions/{Id(id)}/release", request, cancellationToken: cancellationToken);
    }

    private static void ValidateFilters(string? search, string? reason, string? state, string? origin, string parameterName)
    {
        if (search?.Length > 254) throw new ArgumentException("Search cannot exceed 254 characters.", parameterName);
        if (reason is not null && !Reasons.Contains(reason)) throw new ArgumentException("Unsupported suppression reason.", parameterName);
        if (state is not null && !States.Contains(state)) throw new ArgumentException("Unsupported suppression state.", parameterName);
        if (origin is not null && !Origins.Contains(origin)) throw new ArgumentException("Unsupported suppression origin.", parameterName);
    }
}

public sealed class ThemesResource(ViaPostClient client) : ResourceBase(client)
{
    public Task<ThemeList> ListAsync(CancellationToken cancellationToken = default) =>
        Client.RequestAsync<ThemeList>(HttpMethod.Get, "/v1/themes", cancellationToken: cancellationToken);

    public Task<Theme> CreateAsync(CreateThemeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Theme name cannot be empty.", nameof(request));
        if (request.Style.ValueKind != JsonValueKind.Object) throw new ArgumentException("Theme style must be a JSON object.", nameof(request));
        return Client.RequestAsync<Theme>(HttpMethod.Post, "/v1/themes", request, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Client.RequestNoContentAsync(HttpMethod.Delete, $"/v1/themes/{Id(id)}", cancellationToken: cancellationToken);
}
