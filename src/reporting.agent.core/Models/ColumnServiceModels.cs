using System.Text.Json.Serialization;

namespace reporting.agent.core.Models;

public sealed class UniverseSummary
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("enabled")]
    public int Enabled { get; set; }
}

public sealed class ColumnUpdate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("allowedValues")]
    public List<string>? AllowedValues { get; set; }
}

public sealed class ColumnsBulkPatchRequest
{
    [JsonPropertyName("updates")]
    public List<ColumnUpdate> Updates { get; set; } = new();
}

public sealed class ColumnCreateRequest
{
    [JsonPropertyName("column")]
    public ColumnDefinition Column { get; set; } = new();
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public sealed class DuplicateException : Exception
{
    public DuplicateException(string message) : base(message) { }
}

