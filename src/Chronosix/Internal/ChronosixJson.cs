using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chronosix.Internal;

/// <summary>
/// Shared JSON settings for the REST API and the Razor Pages dashboard: camelCase
/// property names and string-valued enums, so the browser sees stable, readable values.
/// </summary>
internal static class ChronosixJson
{
    /// <summary>The serializer options used everywhere Chronosix emits JSON.</summary>
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
