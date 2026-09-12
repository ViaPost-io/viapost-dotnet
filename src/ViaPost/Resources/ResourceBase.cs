using System.Globalization;
namespace ViaPost.Resources;

public abstract class ResourceBase
{
    protected ResourceBase(ViaPostClient client) => Client = client;
    protected ViaPostClient Client { get; }

    protected static string BuildQuery(string path, params (string Name, object? Value)[] values)
    {
        var query = values.Where(pair => pair.Value is not null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Name)}={Uri.EscapeDataString(Format(pair.Value!))}");
        var serialized = string.Join("&", query);
        return serialized.Length == 0 ? path : $"{path}{(path.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{serialized}";
    }

    protected static string Id(Guid id) => Uri.EscapeDataString(id.ToString("D"));

    private static string Format(object value) => value switch
    {
        DateTimeOffset timestamp => timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
