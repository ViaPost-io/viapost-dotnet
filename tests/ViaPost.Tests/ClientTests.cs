using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using ViaPost;
using ViaPost.Models;
using Xunit;

namespace ViaPost.Tests;

public sealed class ClientTests
{
    [Fact]
    public void Constructor_rejects_plain_http_for_non_loopback()
    {
        Assert.Throws<ArgumentException>(() => new ViaPostClient(new ViaPostClientOptions("secret", new Uri("http://example.com"))));
    }

    [Fact]
    public void Constructor_rejects_ambiguous_credentials_and_base_urls()
    {
        Assert.Throws<ArgumentException>(() => new ViaPostClient(" secret"));
        Assert.Throws<ArgumentException>(() => new ViaPostClient(new ViaPostClientOptions("secret", new Uri("https://user@example.com"))));
        Assert.Throws<ArgumentException>(() => new ViaPostClient(new ViaPostClientOptions("secret", new Uri("https://api.example.com?tenant=other"))));
        Assert.Throws<ArgumentException>(() => new ViaPostClient(new ViaPostClientOptions("secret", new Uri("https://api.example.com#fragment"))));
    }

    [Fact]
    public void Default_transport_disables_redirects_and_cookies_and_requires_modern_tls()
    {
        using var handler = ViaPostClient.CreateSecureHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.Equal(DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli, handler.AutomaticDecompression);
        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, handler.SslOptions.EnabledSslProtocols);
    }

    [Fact]
    public async Task Send_sets_auth_user_agent_and_idempotency_without_leaking_key()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.Accepted, """{"accepted":[],"rejected":null}"""));
        using var client = Create(handler);

        await client.Send.SendAsync(new SendEmailRequest("from@example.com", ["to@example.com"]), "idem-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("test-secret", request.AuthorizationParameter);
        Assert.Equal("idem-1", request.IdempotencyKey);
        Assert.Contains("viapost-dotnet/0.1.0", request.UserAgent);
        Assert.DoesNotContain("test-secret", request.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_retries_transient_response_but_mutation_does_not_retry()
    {
        var get = new QueueHandler(
            _ => Json(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"busy","message":"later"}}""", retryAfter: "0"),
            _ => Json(HttpStatusCode.OK, """{"messages":[]}"""));
        using (var client = Create(get))
        {
            await client.Messages.ListAsync();
            Assert.Equal(2, get.Requests.Count);
        }

        var post = new QueueHandler(_ => Json(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"busy","message":"later"}}"""));
        using var postClient = Create(post);
        await Assert.ThrowsAsync<ViaPostApiException>(() => postClient.Send.SendAsync(new SendEmailRequest("from@example.com", ["to@example.com"])));
        Assert.Single(post.Requests);
    }

    [Fact]
    public async Task Oversized_decoded_response_is_rejected()
    {
        var payload = new string('x', ViaPostClientOptions.DefaultMaximumResponseBytes + 1);
        var handler = new QueueHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) });
        using var client = Create(handler);

        await Assert.ThrowsAsync<ViaPostResponseTooLargeException>(() => client.Messages.ListAsync());
    }

    [Fact]
    public async Task Api_error_is_typed_and_redacts_api_key()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":{"code":"validation_error","message":"bad test-secret","request_id":"req_1"}}"""));
        using var client = Create(handler);

        var error = await Assert.ThrowsAsync<ViaPostApiException>(() => client.Messages.ListAsync());
        Assert.Equal("validation_error", error.ErrorCode);
        Assert.Equal("req_1", error.RequestId);
        Assert.DoesNotContain("test-secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_error_redacts_key_from_every_diagnostic_field()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.BadRequest,
            """{"error":{"code":"bad-test-secret","message":"bad test-secret","request_id":"req-test-secret"}}"""));
        using var client = Create(handler);

        var error = await Assert.ThrowsAsync<ViaPostApiException>(() => client.Messages.ListAsync());

        Assert.DoesNotContain("test-secret", error.ErrorCode, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", error.RequestId, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Idempotency_key_rejects_header_controls_before_network()
    {
        var handler = new QueueHandler(_ => throw new InvalidOperationException("network must not be used"));
        using var client = Create(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Send.SendAsync(
            new SendEmailRequest("from@example.com", ["to@example.com"]),
            "safe\r\nInjected: value"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Send_validates_contract_before_network()
    {
        var handler = new QueueHandler(_ => throw new InvalidOperationException("network must not be used"));
        using var client = Create(handler);
        var request = new SendEmailRequest("from@example.com", []);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Send.SendAsync(request));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Resources_build_expected_paths_and_query_pagination()
    {
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, """{"domains":[]}"""),
            _ => Json(HttpStatusCode.OK, """{"templates":[]}"""),
            _ => Json(HttpStatusCode.OK, """{"webhooks":[]}"""),
            _ => Json(HttpStatusCode.OK, """{"data":[]}"""),
            _ => Json(HttpStatusCode.OK, """{"period":{"start":"2026-01-01T00:00:00Z","end":"2026-02-01T00:00:00Z","timezone":"UTC"},"used":0,"limit":100,"remaining":100,"unlimited":false}"""));
        using var client = Create(handler);

        await client.Domains.ListAsync();
        await client.Templates.ListAsync(new TemplateListOptions(Cursor: DateTimeOffset.Parse("2026-01-01T00:00:00Z"), Limit: 25, Search: "welcome"));
        await client.Webhooks.ListAsync();
        await client.Automations.ListAsync(new AutomationListOptions("enabled", "onboarding"));
        await client.Usage.GetAsync();

        Assert.Collection(handler.Requests,
            r => Assert.EndsWith("/v1/domains", r.Uri, StringComparison.Ordinal),
            r => Assert.Contains("/v1/templates?", r.Uri, StringComparison.Ordinal),
            r => Assert.EndsWith("/v1/webhooks", r.Uri, StringComparison.Ordinal),
            r => Assert.Contains("/v1/automations?", r.Uri, StringComparison.Ordinal),
            r => Assert.EndsWith("/v1/usage", r.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Message_filters_match_the_OpenAPI_contract()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, """{"messages":[]}"""));
        using var client = Create(handler);
        var apiKeyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await client.Messages.ListAsync(new MessageListOptions(
            Cursor: DateTimeOffset.Parse("2026-01-01T00:00:00Z"), Limit: 25, Status: "delivered",
            Search: "welcome", Period: "7d", ApiKeyId: apiKeyId));

        var uri = Assert.Single(handler.Requests).Uri;
        Assert.Contains("status=delivered", uri, StringComparison.Ordinal);
        Assert.Contains("search=welcome", uri, StringComparison.Ordinal);
        Assert.Contains("period=7d", uri, StringComparison.Ordinal);
        Assert.Contains("api_key_id=11111111-1111-1111-1111-111111111111", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("stream=", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("domain_id=", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = Create(new AsyncHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Json(HttpStatusCode.OK, "{}");
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Messages.ListAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Configured_timeout_has_a_typed_error()
    {
        using var client = new ViaPostClient(
            new ViaPostClientOptions("test-secret", new Uri("https://api.example.test")) { Timeout = TimeSpan.FromMilliseconds(10) },
            new HttpClient(new AsyncHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Json(HttpStatusCode.OK, "{}");
            })));

        await Assert.ThrowsAsync<ViaPostTimeoutException>(() => client.Messages.ListAsync());
    }

    [Fact]
    public async Task Timeout_covers_retry_delay_and_the_whole_operation()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"busy","message":"later"}}""", retryAfter: "30"));
        using var client = new ViaPostClient(
            new ViaPostClientOptions("test-secret", new Uri("https://api.example.test")) { Timeout = TimeSpan.FromMilliseconds(20) },
            new HttpClient(handler));

        await Assert.ThrowsAsync<ViaPostTimeoutException>(() => client.Messages.ListAsync());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Critical_template_and_webhook_constraints_are_local()
    {
        var handler = new QueueHandler(_ => throw new InvalidOperationException("network must not be used"));
        using var client = Create(handler);
        var graph = JsonDocument.Parse("{}").RootElement.Clone();

        await Assert.ThrowsAsync<ArgumentException>(() => client.Templates.UpdateDraftAsync(Guid.NewGuid(), new UpdateTemplateDraftRequest(graph, []) { ExpectedVersionId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Templates.PreviewAsync(Guid.NewGuid(), new PreviewTemplateRequest { Variables = Enumerable.Range(0, 101).ToDictionary(x => x.ToString(), x => (object?)x) }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Webhooks.CreateAsync(new CreateWebhookRequest(new Uri("ftp://example.test"), ["message.delivered"])));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Webhooks.CreateAsync(new CreateWebhookRequest(new Uri("https://example.test"), [])));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void One_time_webhook_secret_is_redacted_from_string_representation()
    {
        const string secret = "whsec_never_log_this";
        var response = new CreateWebhookResponse { Secret = secret };

        Assert.DoesNotContain(secret, response.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", response.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Temporary_asset_upload_credentials_are_redacted_from_string_representation()
    {
        var policy = new TemplateAssetPolicy(
            new Uri("https://storage.example.test/upload?signature=never-log-this"),
            new Dictionary<string, string> { ["policy"] = "signed-policy" },
            new Uri("https://cdn.example.test/public.png"));

        Assert.DoesNotContain("never-log-this", policy.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("signed-policy", policy.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", policy.ToString(), StringComparison.Ordinal);
    }

    private static ViaPostClient Create(HttpMessageHandler handler) =>
        new(new ViaPostClientOptions("test-secret", new Uri("https://api.example.test")), new HttpClient(handler));

    private static HttpResponseMessage Json(HttpStatusCode status, string body, string? retryAfter = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (retryAfter is not null) response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        return response;
    }

    private sealed class QueueHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(responses);
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null,
                request.Headers.UserAgent.ToString()));
            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private sealed record CapturedRequest(string Uri, string? AuthorizationScheme, string? AuthorizationParameter, string? IdempotencyKey, string UserAgent);

    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }
}
