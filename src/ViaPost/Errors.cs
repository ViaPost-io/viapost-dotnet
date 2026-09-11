using System.Net;

namespace ViaPost;

public class ViaPostException : Exception
{
    public ViaPostException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public sealed class ViaPostApiException : ViaPostException
{
    internal ViaPostApiException(HttpStatusCode statusCode, string errorCode, string message, string? requestId, TimeSpan? retryAfter)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        RequestId = requestId;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }
    public string? RequestId { get; }
    public TimeSpan? RetryAfter { get; }
}

public sealed class ViaPostTimeoutException : ViaPostException
{
    internal ViaPostTimeoutException(TimeSpan timeout, Exception innerException)
        : base($"ViaPost request exceeded the configured timeout of {timeout.TotalSeconds:g} seconds.", innerException) => Timeout = timeout;
    public TimeSpan Timeout { get; }
}

public sealed class ViaPostTransportException : ViaPostException
{
    internal ViaPostTransportException(Exception innerException) : base("Unable to complete the ViaPost request.", innerException) { }
}

public sealed class ViaPostResponseTooLargeException : ViaPostException
{
    internal ViaPostResponseTooLargeException(int maximumBytes) : base($"ViaPost response exceeded the configured limit of {maximumBytes} bytes.") => MaximumBytes = maximumBytes;
    public int MaximumBytes { get; }
}

public sealed class ViaPostSerializationException : ViaPostException
{
    internal ViaPostSerializationException(Exception innerException) : base("ViaPost returned a response that could not be decoded.", innerException) { }
}
