using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using ViaPost.Resources;

namespace ViaPost;

public sealed class ViaPostClient : IDisposable
{
    public const string SdkVersion = "0.1.0";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ViaPostClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ViaPostClient(string apiKey) : this(new ViaPostClientOptions(apiKey)) { }

    public ViaPostClient(ViaPostClientOptions options, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient(CreateSecureHandler(), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        Send = new SendResource(this);
        Messages = new MessagesResource(this);
        Domains = new DomainsResource(this);
        Templates = new TemplatesResource(this);
        Webhooks = new WebhooksResource(this);
        Automations = new AutomationsResource(this);
        Usage = new UsageResource(this);
    }

    public SendResource Send { get; }
    public MessagesResource Messages { get; }
    public DomainsResource Domains { get; }
    public TemplatesResource Templates { get; }
    public WebhooksResource Webhooks { get; }
    public AutomationsResource Automations { get; }
    public UsageResource Usage { get; }

    internal async Task<T> RequestAsync<T>(HttpMethod method, string path, object? body = null, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var responseBytes = await RequestBytesAsync(method, path, body, idempotencyKey, "application/json", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (typeof(T) == typeof(EmptyResponse)) return (T)(object)new EmptyResponse();
        try
        {
            return JsonSerializer.Deserialize<T>(responseBytes, JsonOptions)
                ?? throw new JsonException("Response contained JSON null.");
        }
        catch (JsonException exception)
        {
            throw new ViaPostSerializationException(exception);
        }
    }

    internal async Task<byte[]> RequestBytesAsync(HttpMethod method, string path, object? body = null, string? idempotencyKey = null, string accept = "application/octet-stream", int? maximumResponseBytes = null, CancellationToken cancellationToken = default)
    {
        var payload = body is null ? null : JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        var retryableMethod = method == HttpMethod.Get || method == HttpMethod.Head;
        var attempts = retryableMethod ? _options.MaximumGetAttempts : 1;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var request = CreateRequest(method, path, payload, idempotencyKey, accept);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                var retryAfter = GetRetryAfter(response);
                if (attempt < attempts && IsTransient(response.StatusCode))
                {
                    response.Dispose();
                    await Task.Delay(retryAfter ?? RetryBackoff(attempt), timeout.Token).ConfigureAwait(false);
                    continue;
                }

                var responseLimit = response.IsSuccessStatusCode
                    ? maximumResponseBytes ?? _options.MaximumResponseBytes
                    : _options.MaximumResponseBytes;
                var responseBytes = await ReadLimitedAsync(response.Content, responseLimit, timeout.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw CreateApiException(response, responseBytes, retryAfter);
                return responseBytes;
            }
            catch (ViaPostException) { throw; }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ViaPostTimeoutException(_options.Timeout, exception);
            }
            catch (HttpRequestException exception)
            {
                throw new ViaPostTransportException(exception);
            }
        }

        throw new InvalidOperationException("Unreachable retry state.");
    }

    internal async Task RequestNoContentAsync(HttpMethod method, string path, object? body = null, CancellationToken cancellationToken = default) =>
        _ = await RequestAsync<EmptyResponse>(method, path, body, cancellationToken: cancellationToken).ConfigureAwait(false);

    internal Task<byte[]> RequestRawMessageAsync(string path, CancellationToken cancellationToken = default) =>
        RequestBytesAsync(HttpMethod.Get, path, accept: "message/rfc822", cancellationToken: cancellationToken, maximumResponseBytes: _options.MaximumRawMessageBytes);

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    internal static SocketsHttpHandler CreateSecureHandler()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };
        handler.SslOptions.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        return handler;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, byte[]? body, string? idempotencyKey, string accept)
    {
        var request = new HttpRequestMessage(method, new Uri(_options.BaseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.UserAgent.ParseAdd($"viapost-dotnet/{SdkVersion}");
        request.Headers.Accept.ParseAdd(accept);
        request.Headers.TryAddWithoutValidation("X-ViaPost-SDK", "dotnet");
        if (idempotencyKey is not null)
        {
            if (idempotencyKey.Length is < 1 or > 255 || idempotencyKey.Any(character => character is < '!' or > '~'))
                throw new ArgumentException("Idempotency key must contain 1 to 255 visible ASCII characters.", nameof(idempotencyKey));
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }
        if (body is not null) request.Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };
        return request;
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int maximumResponseBytes, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maximumResponseBytes) throw new ViaPostResponseTooLargeException(maximumResponseBytes);
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream(Math.Min(maximumResponseBytes, 64 * 1024));
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) return output.ToArray();
            if (output.Length + count > maximumResponseBytes) throw new ViaPostResponseTooLargeException(maximumResponseBytes);
            output.Write(buffer, 0, count);
        }
    }

    private ViaPostApiException CreateApiException(HttpResponseMessage response, byte[] bytes, TimeSpan? retryAfter)
    {
        string code = "api_error";
        string message = $"ViaPost API returned HTTP {(int)response.StatusCode}.";
        string? requestId = null;
        try
        {
            using var json = JsonDocument.Parse(bytes);
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("code", out var codeElement)) code = codeElement.GetString() ?? code;
                if (error.TryGetProperty("message", out var messageElement)) message = messageElement.GetString() ?? message;
                if (error.TryGetProperty("request_id", out var requestElement)) requestId = requestElement.GetString();
            }
        }
        catch (JsonException) { }
        code = code.Replace(_options.ApiKey, "[REDACTED]", StringComparison.Ordinal);
        message = message.Replace(_options.ApiKey, "[REDACTED]", StringComparison.Ordinal);
        requestId = requestId?.Replace(_options.ApiKey, "[REDACTED]", StringComparison.Ordinal);
        return new ViaPostApiException(response.StatusCode, code, message, requestId, retryAfter);
    }

    private TimeSpan RetryBackoff(int attempt) => TimeSpan.FromMilliseconds(Math.Min(250 * Math.Pow(2, attempt - 1), _options.MaximumRetryDelay.TotalMilliseconds));

    private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var value = response.Headers.RetryAfter;
        var delay = value?.Delta ?? (value?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        if (delay is null) return null;
        if (delay < TimeSpan.Zero) return TimeSpan.Zero;
        return delay > _options.MaximumRetryDelay ? _options.MaximumRetryDelay : delay;
    }

    private static bool IsTransient(HttpStatusCode status) => status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    internal sealed class EmptyResponse;
}
