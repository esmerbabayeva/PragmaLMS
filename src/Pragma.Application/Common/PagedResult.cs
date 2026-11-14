using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pragma.Application.Common;

/// <summary>
/// Bütün siyahı və kataloq sorğuları üçün universal səhifələmə (Pagination) modeli.
/// </summary>
/// <typeparam name="T">Məlumat tipi</typeparam>
[JsonConverter(typeof(PagedResultJsonConverterFactory))]
public class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages { get; }

    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResult(IReadOnlyCollection<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;
    }

    public static PagedResult<T> Create(IReadOnlyCollection<T> items, int totalCount, int pageNumber, int pageSize)
    {
        return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
    }
}

public class PagedResultJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(PagedResult<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var itemType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(PagedResultJsonConverter<>).MakeGenericType(itemType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public class PagedResultJsonConverter<T> : JsonConverter<PagedResult<T>>
{
    public override PagedResult<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var items = JsonSerializer.Deserialize<List<T>>(ref reader, options) ?? [];
            return new PagedResult<T>(items, items.Count, 1, Math.Max(1, items.Count));
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartArray or StartObject, but got {reader.TokenType}");
        }

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var itemsList = new List<T>();
        if (root.TryGetProperty("items", out var itemsProp) || root.TryGetProperty("Items", out itemsProp))
        {
            itemsList = itemsProp.Deserialize<List<T>>(options) ?? [];
        }

        int totalCount = itemsList.Count;
        if (root.TryGetProperty("totalCount", out var tcProp) || root.TryGetProperty("TotalCount", out tcProp))
        {
            totalCount = tcProp.GetInt32();
        }

        int pageNumber = 1;
        if (root.TryGetProperty("pageNumber", out var pnProp) || root.TryGetProperty("PageNumber", out pnProp))
        {
            pageNumber = pnProp.GetInt32();
        }
        else if (root.TryGetProperty("page", out var pProp) || root.TryGetProperty("Page", out pProp))
        {
            pageNumber = pProp.GetInt32();
        }

        int pageSize = itemsList.Count > 0 ? itemsList.Count : 50;
        if (root.TryGetProperty("pageSize", out var psProp) || root.TryGetProperty("PageSize", out psProp))
        {
            pageSize = psProp.GetInt32();
        }

        return new PagedResult<T>(itemsList, totalCount, pageNumber, pageSize);
    }

    public override void Write(Utf8JsonWriter writer, PagedResult<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("items");
        JsonSerializer.Serialize(writer, value.Items, options);
        writer.WriteNumber("totalCount", value.TotalCount);
        writer.WriteNumber("pageNumber", value.PageNumber);
        writer.WriteNumber("pageSize", value.PageSize);
        writer.WriteNumber("totalPages", value.TotalPages);
        writer.WriteBoolean("hasPreviousPage", value.HasPreviousPage);
        writer.WriteBoolean("hasNextPage", value.HasNextPage);
        writer.WriteEndObject();
    }
}
