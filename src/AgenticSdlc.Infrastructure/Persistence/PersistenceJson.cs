// JSON options dùng chung khi serialize artifact/graph vào cột jsonb.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticSdlc.Infrastructure.Persistence;

internal static class PersistenceJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
