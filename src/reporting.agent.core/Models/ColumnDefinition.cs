using System.Text.Json.Serialization;

namespace reporting.agent.core.Models;

/// Raw column definition as stored in resources/columns/*.json.
public sealed class ColumnDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "String";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("defaultOrder")]
    public int DefaultOrder { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("linkedIds")]
    public List<string>? LinkedIds { get; set; }

    [JsonPropertyName("allowedValues")]
    public List<string>? AllowedValues { get; set; }
}

