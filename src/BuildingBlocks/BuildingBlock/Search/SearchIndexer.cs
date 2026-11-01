using System.Text.Json;
using BuildingBlock.Models;
using Microsoft.Extensions.Logging;

namespace BuildingBlock.Search;

public sealed class SearchIndexer<T>(ISearchStore<T> store, ILogger<SearchIndexer<T>> logger) where T : BaseEntity
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task ApplyAsync(string json, string topic = "", CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("payload", out var payload))
            {
                root = payload;
            }

            var op = root.TryGetProperty("op", out var opElement) ? opElement.GetString() : null;
            var after = TryRead(root, "after");
            var before = TryRead(root, "before");
            var item = after ?? before;
            if (item is null)
            {
                return;
            }

            var remove = op is "d" || item.IsDeleted || !item.IsActive;
            if (remove)
            {
                await store.DeleteAsync(item.Id, cancellationToken);
                return;
            }

            await store.UpsertAsync(item, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Search sync failed for topic {Topic}\n{StackTrace}",
                topic,
                ex.ToString());
            throw;
        }
    }

    private static T? TryRead(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return default;
        }

        if (element.TryGetProperty("data", out var data))
        {
            var raw = data.ValueKind == JsonValueKind.String ? data.GetString() : data.GetRawText();
            return string.IsNullOrWhiteSpace(raw) ? default : JsonSerializer.Deserialize<T>(raw, JsonOptions);
        }

        return JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions);
    }
}
