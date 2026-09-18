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
        Assert.Throws<ArgumentException>(() => new ViaPostClient(new ViaPostClientOptions("secret", new Uri("https://status.viapost.io"))));
    }

    [Fact]
    public void Client_options_do_not_expose_or_serialize_the_api_key()
    {
        const string secret = "vp_never_serialize_this";
        var options = new ViaPostClientOptions(secret);

        Assert.Null(typeof(ViaPostClientOptions).GetProperty("ApiKey"));
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(options), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, options.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", options.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("viapost-dotnet/0.2.0", request.UserAgent);
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
    public async Task Raw_message_uses_its_40_mib_limit_without_raising_the_json_limit()
    {
        var raw = new byte[ViaPostClientOptions.DefaultMaximumResponseBytes + 1];
        var handler = new QueueHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(raw) });
        using var client = Create(handler);

        var downloaded = await client.Messages.GetRawAsync(Guid.NewGuid());

        Assert.Equal(raw.Length, downloaded.Length);
        Assert.Equal(40 * 1024 * 1024, ViaPostClientOptions.DefaultMaximumRawMessageBytes);
    }

    [Fact]
    public async Task Configured_raw_message_limit_is_enforced()
    {
        var handler = new QueueHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1025]) });
        using var client = new ViaPostClient(
            new ViaPostClientOptions("test-secret", new Uri("https://api.example.test")) { MaximumRawMessageBytes = 1024 },
            new HttpClient(handler));

        var error = await Assert.ThrowsAsync<ViaPostResponseTooLargeException>(() => client.Messages.GetRawAsync(Guid.NewGuid()));

        Assert.Contains("1024", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Suppression_csv_export_uses_its_40_mib_limit_without_raising_the_json_limit()
    {
        var csv = new byte[ViaPostClientOptions.DefaultMaximumResponseBytes + 1];
        var handler = new QueueHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(csv) });
        using var client = Create(handler);

        var downloaded = await client.Suppressions.ExportAsync();

        Assert.Equal(csv.Length, downloaded.Length);
        Assert.Equal(40 * 1024 * 1024, ViaPostClientOptions.DefaultMaximumExportBytes);
        Assert.Equal("text/csv", Assert.Single(handler.Requests).Accept);
    }

    [Fact]
    public async Task Configured_suppression_csv_export_limit_is_enforced()
    {
        var handler = new QueueHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1025]) });
        using var client = new ViaPostClient(
            new ViaPostClientOptions("test-secret", new Uri("https://api.example.test")) { MaximumExportBytes = 1024 },
            new HttpClient(handler));

        var error = await Assert.ThrowsAsync<ViaPostResponseTooLargeException>(() => client.Suppressions.ExportAsync());

        Assert.Equal(1024, error.MaximumBytes);
    }

    [Fact]
    public void Configured_raw_response_limits_reject_values_above_the_defensive_ceiling()
    {
        var rawOptions = new ViaPostClientOptions("test-secret")
        {
            MaximumRawMessageBytes = ViaPostClientOptions.MaximumRawResponseBytesLimit + 1
        };
        var exportOptions = new ViaPostClientOptions("test-secret")
        {
            MaximumExportBytes = ViaPostClientOptions.MaximumRawResponseBytesLimit + 1
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new ViaPostClient(rawOptions));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViaPostClient(exportOptions));
    }

    [Fact]
    public void Configured_json_response_limit_rejects_values_above_the_defensive_ceiling()
    {
        var options = new ViaPostClientOptions("test-secret")
        {
            MaximumResponseBytes = ViaPostClientOptions.MaximumResponseBytesLimit + 1
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new ViaPostClient(options));
    }

    [Fact]
    public async Task Raw_message_errors_keep_the_json_response_limit()
    {
        var payload = new byte[ViaPostClientOptions.DefaultMaximumResponseBytes + 1];
        var handler = new QueueHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new ByteArrayContent(payload) });
        using var client = Create(handler);

        var error = await Assert.ThrowsAsync<ViaPostResponseTooLargeException>(() => client.Messages.GetRawAsync(Guid.NewGuid()));

        Assert.Equal(ViaPostClientOptions.DefaultMaximumResponseBytes, error.MaximumBytes);
    }

    [Fact]
    public async Task Small_raw_limit_does_not_hide_a_bounded_api_error()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":{"code":"raw_unavailable","message":"not available"}}"""));
        using var client = new ViaPostClient(
            new ViaPostClientOptions("test-secret", new Uri("https://api.example.test")) { MaximumRawMessageBytes = 1 },
            new HttpClient(handler));

        var error = await Assert.ThrowsAsync<ViaPostApiException>(() => client.Messages.GetRawAsync(Guid.NewGuid()));

        Assert.Equal("raw_unavailable", error.ErrorCode);
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
    public async Task Api_error_redacts_named_secrets_from_every_diagnostic_field()
    {
        const string secret = "whsec_never_log_this";
        var body = JsonSerializer.Serialize(new
        {
            error = new { code = "invalid", message = $"failed {secret}", request_id = $"req-{secret}", secret }
        });
        var handler = new QueueHandler(_ => Json(HttpStatusCode.BadRequest, body));
        using var client = Create(handler);

        var error = await Assert.ThrowsAsync<ViaPostApiException>(() => client.Messages.ListAsync());

        Assert.DoesNotContain(secret, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, error.ErrorCode, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, error.RequestId, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
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
        foreach (var url in new[]
        {
            "http://example.test/hook",
            "https://user:password@example.test/hook",
            "https://example.test/hook#fragment",
            "https://localhost/hook",
            "https://localhost./hook",
            "https://api.localhost/hook",
            "https://127.0.0.1/hook",
            "https://127.1/hook",
            "https://2130706433/hook",
            "https://0x7f000001/hook",
            "https://10.0.0.1/hook",
            "https://100.64.0.1/hook",
            "https://169.254.1.1/hook",
            "https://192.0.2.1/hook",
            "https://198.18.0.1/hook",
            "https://203.0.113.1/hook",
            "https://240.0.0.1/hook",
            "https://[::1]/hook",
            "https://[::127.0.0.1]/hook",
            "https://[::ffff:127.0.0.1]/hook",
            "https://[fc00::1]/hook"
        })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => client.Webhooks.CreateAsync(new CreateWebhookRequest(new Uri(url), ["delivered"])));
        }
        await Assert.ThrowsAsync<ArgumentException>(() => client.Webhooks.CreateAsync(new CreateWebhookRequest(new Uri("https://example.test/hook"), ["delivered", "delivered"])));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Webhooks.UpdateAsync(Guid.NewGuid(), new UpdateWebhookRequest(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Webhooks.TestAsync(Guid.NewGuid(), null!));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Webhooks.ReplayAsync(Guid.NewGuid(), Guid.NewGuid(), string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Webhooks.RotateSecretAsync(Guid.NewGuid(), "bad\r\nkey"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void One_time_webhook_secret_is_redacted_from_string_representation()
    {
        const string secret = "whsec_never_log_this";
        var response = new CreateWebhookResponse { Secret = SensitiveString.From(secret) };

        Assert.DoesNotContain(secret, response.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", response.ToString(), StringComparison.Ordinal);
        Assert.Equal(secret, response.Secret.Reveal());
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(response), StringComparison.Ordinal);
    }

    [Fact]
    public void Message_content_and_webhook_urls_are_redacted_from_string_representation()
    {
        var detail = new MessageDetail
        {
            Id = Guid.NewGuid(),
            BodyHtml = "<p>private body</p>",
            BodyPlain = "private body"
        };
        var request = new CreateWebhookRequest(new Uri("https://example.test/hook?token=never-log-this"), ["delivered"]);
        var endpoint = new WebhookEndpoint { Url = request.Url };

        Assert.DoesNotContain("private body", detail.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("never-log-this", request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", endpoint.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Message_detail_and_raw_download_follow_the_content_contract()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var raw = "From: hello@example.com\r\nTo: person@example.com\r\n\r\nHello\r\n";
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, $$"""{"id":"{{id}}","status":"delivered","stream":"transactional","from_address":"hello@example.com","to_address":"person@example.com","recipient_domain":"example.com","created_at":"2026-09-16T00:00:00Z","body_plain":"Hello","content_status":"available","raw_message_api_path":"/v1/messages/{{id}}/raw","content_variant":"submitted"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(raw)) });
        using var client = Create(handler);

        var detail = await client.Messages.GetAsync(id);
        var downloaded = await client.Messages.GetRawAsync(id);

        Assert.Equal("Hello", detail.BodyPlain);
        Assert.Equal("available", detail.ContentStatus);
        Assert.Equal(Encoding.UTF8.GetBytes(raw), downloaded);
        Assert.Equal("application/json", handler.Requests[0].Accept);
        Assert.Equal("message/rfc822", handler.Requests[1].Accept);
    }

    [Fact]
    public async Task Scheduled_send_serializes_the_contract_timestamp_field()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.Accepted, "{}"));
        using var client = Create(handler);
        var scheduledAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await client.Send.SendAsync(new SendEmailRequest("from@example.com", ["to@example.com"])
        {
            Text = "scheduled",
            ScheduledAt = scheduledAt
        }, "scheduled-1");

        using var document = JsonDocument.Parse(handler.Requests.Single().Body);
        Assert.Equal("2030-01-02T03:04:05+00:00", document.RootElement.GetProperty("scheduled_at").GetString());
        Assert.Equal("scheduled", document.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Message_and_segment_variants_deserialize_the_contract_shapes()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, $$"""{"id":"{{id}}","status":"cancelled","stream":"transactional","from_address":"from@example.com","to_address":"to@example.com","recipient_domain":"example.com","created_at":"2026-09-16T00:00:00Z","scheduled_at":"2026-09-17T00:00:00Z","cancelled_at":"2026-09-16T01:00:00Z","suppressed_at":null}"""),
            _ => Json(HttpStatusCode.OK, $$"""{"id":"{{id}}","name":"Static","description":null,"kind":"static","definition":null,"contact_count":2,"created_at":"2026-09-16T00:00:00Z","updated_at":"2026-09-16T00:00:01Z"}"""),
            _ => Json(HttpStatusCode.OK, $$"""{"id":"{{id}}","name":"Dynamic","description":"rule based","kind":"dynamic","definition":{"operator":"all","rules":[]},"contact_count":3,"created_at":"2026-09-16T00:00:00Z","updated_at":"2026-09-16T00:00:01Z"}"""));
        using var client = Create(handler);

        var message = await client.Messages.CancelAsync(id);
        var staticSegment = await client.Segments.GetAsync(id);
        var dynamicSegment = await client.Segments.GetAsync(id);

        Assert.Equal(DateTimeOffset.Parse("2026-09-17T00:00:00Z"), message.ScheduledAt);
        Assert.Equal(DateTimeOffset.Parse("2026-09-16T01:00:00Z"), message.CancelledAt);
        Assert.Null(message.SuppressedAt);
        var staticShape = Assert.IsType<StaticSegment>(staticSegment);
        Assert.Equal("static", staticShape.Kind);
        Assert.True(staticShape.Definition is null || staticShape.Definition.Value.ValueKind == JsonValueKind.Null);
        var dynamicShape = Assert.IsType<DynamicSegment>(dynamicSegment);
        Assert.Equal("dynamic", dynamicShape.Kind);
        Assert.Equal("all", dynamicShape.Definition.GetProperty("operator").GetString());
    }

    [Fact]
    public async Task Static_and_dynamic_segment_requests_serialize_discriminated_shapes()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, $$"""{"id":"{{id}}","name":"Static","description":null,"kind":"static","definition":null,"contact_count":0,"created_at":"2026-09-16T00:00:00Z","updated_at":"2026-09-16T00:00:00Z"}"""),
            _ => Json(HttpStatusCode.OK, $$"""{"id":"{{id}}","name":"Dynamic","description":null,"kind":"dynamic","definition":{"operator":"any","rules":[]},"contact_count":0,"created_at":"2026-09-16T00:00:00Z","updated_at":"2026-09-16T00:00:00Z"}"""));
        using var client = Create(handler);
        var definition = JsonDocument.Parse("{\"operator\":\"any\",\"rules\":[]}").RootElement.Clone();

        await client.Segments.CreateAsync(new StaticSegmentCreateRequest("Static"));
        await client.Segments.CreateAsync(new DynamicSegmentCreateRequest("Dynamic", definition));

        using var staticBody = JsonDocument.Parse(handler.Requests[0].Body);
        using var dynamicBody = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal("static", staticBody.RootElement.GetProperty("kind").GetString());
        Assert.False(staticBody.RootElement.TryGetProperty("definition", out _));
        Assert.Equal("dynamic", dynamicBody.RootElement.GetProperty("kind").GetString());
        Assert.Equal("any", dynamicBody.RootElement.GetProperty("definition").GetProperty("operator").GetString());
    }

    [Fact]
    public async Task Webhook_operations_send_expected_idempotency_keys()
    {
        var endpointId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var deliveryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.Accepted, $$"""{"delivery_id":"{{Guid.NewGuid()}}","status":"queued","created_at":"2026-09-16T00:00:00Z","is_test":true}"""),
            _ => Json(HttpStatusCode.Accepted, $$"""{"delivery_id":"{{Guid.NewGuid()}}","status":"queued","created_at":"2026-09-16T00:00:00Z","source_delivery_id":"{{deliveryId}}"}"""),
            _ => Json(HttpStatusCode.OK, $$"""{"endpoint":{"id":"{{endpointId}}","url":"https://example.test/hook","event_types":["delivered"],"enabled":true,"max_attempts":8,"consecutive_failures":0,"disabled_at":null,"secret_rotated_at":null,"version":2,"created_at":"2026-09-16T00:00:00Z","updated_at":"2026-09-16T00:00:00Z"},"secret":"{{new string('s', 43)}}","rotated_at":"2026-09-16T00:00:00Z"}"""));
        using var client = Create(handler);

        var tested = await client.Webhooks.TestAsync(endpointId, "test-operation-1");
        var replayed = await client.Webhooks.ReplayAsync(endpointId, deliveryId, "replay-operation-1");
        var rotated = await client.Webhooks.RotateSecretAsync(endpointId, "rotate-operation-1");

        Assert.True(tested.IsTest);
        Assert.Equal(deliveryId, replayed.SourceDeliveryId);
        Assert.Equal(43, rotated.Secret?.Reveal().Length);
        Assert.Collection(handler.Requests,
            request => Assert.Equal("test-operation-1", request.IdempotencyKey),
            request => Assert.Equal("replay-operation-1", request.IdempotencyKey),
            request => Assert.Equal("rotate-operation-1", request.IdempotencyKey));
        Assert.All(handler.Requests, request => Assert.Equal("{}", request.Body));
        Assert.DoesNotContain(new string('s', 43), rotated.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(new string('s', 43), JsonSerializer.Serialize(rotated), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_delivery_models_deserialize_the_closed_redacted_payload_contract()
    {
        var endpointId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var deliveryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var messageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var body = $$"""
            {
              "delivery_id":"{{deliveryId}}",
              "event_type":"delivered",
              "status":"delivered",
              "attempt_count":1,
              "created_at":"2026-09-16T00:00:00Z",
              "updated_at":"2026-09-16T00:00:01Z",
              "delivered_at":"2026-09-16T00:00:01Z",
              "last_response_code":204,
              "last_duration_ms":12,
              "is_test":false,
              "payload_redacted":{
                "event_type":"delivered",
                "message_id":"{{messageId}}",
                "occurred_at":"2026-09-16T00:00:00Z",
                "test":false
              },
              "attempts":[{
                "attempt":1,
                "status":"delivered",
                "response_code":204,
                "duration_ms":12,
                "attempted_at":"2026-09-16T00:00:01Z"
              }]
            }
            """;
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, body));
        using var client = Create(handler);

        var delivery = await client.Webhooks.GetDeliveryAsync(endpointId, deliveryId);

        Assert.Equal(messageId, delivery.PayloadRedacted.MessageId);
        Assert.Equal("delivered", delivery.PayloadRedacted.EventType);
        Assert.False(delivery.PayloadRedacted.Test);
        Assert.Equal(204, Assert.Single(delivery.Attempts).ResponseCode);
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
                request.Method.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null,
                request.Headers.UserAgent.ToString(),
                request.Headers.Accept.SingleOrDefault()?.MediaType,
                request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult()));
            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private sealed record CapturedRequest(string Method, string Uri, string? AuthorizationScheme, string? AuthorizationParameter, string? IdempotencyKey, string UserAgent, string? Accept, string? Body);

    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }
}
