using System.Net;

namespace ViaPost;

public sealed class ViaPostClientOptions
{
    public static readonly Uri DefaultBaseUri = new("https://api.viapost.io");
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    public const int DefaultMaximumResponseBytes = 8 * 1024 * 1024;
    public const int DefaultMaximumRawMessageBytes = 40 * 1024 * 1024;

    public ViaPostClientOptions(string apiKey, Uri? baseUri = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey != apiKey.Trim()) throw new ArgumentException("API key cannot be empty or contain surrounding whitespace.", nameof(apiKey));
        if (apiKey.Any(char.IsControl)) throw new ArgumentException("API key cannot contain control characters.", nameof(apiKey));
        ApiKey = apiKey;
        BaseUri = NormalizeBaseUri(baseUri ?? DefaultBaseUri);
    }

    public string ApiKey { get; }
    public Uri BaseUri { get; }
    public TimeSpan Timeout { get; init; } = DefaultTimeout;
    public int MaximumResponseBytes { get; init; } = DefaultMaximumResponseBytes;
    public int MaximumRawMessageBytes { get; init; } = DefaultMaximumRawMessageBytes;
    public int MaximumGetAttempts { get; init; } = 3;
    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        if (Timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Timeout));
        if (MaximumResponseBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaximumResponseBytes));
        if (MaximumRawMessageBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaximumRawMessageBytes));
        if (MaximumGetAttempts is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(MaximumGetAttempts));
        if (MaximumRetryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaximumRetryDelay));
    }

    private static Uri NormalizeBaseUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri) throw new ArgumentException("Base URL must be absolute.", nameof(uri));
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Base URL cannot contain credentials, query, or fragment.", nameof(uri));
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri)))
            throw new ArgumentException("Base URL must use HTTPS. Plain HTTP is allowed only for loopback development.", nameof(uri));
        return new Uri(uri.AbsoluteUri.TrimEnd('/'), UriKind.Absolute);
    }

    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address));
}
