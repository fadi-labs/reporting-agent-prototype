using System.Text.Json.Serialization;

namespace reporting.agent.core.Models;

public sealed class FieldResult
{
    [JsonPropertyName("column_id")]
    public string ColumnId { get; set; } = "";

    [JsonPropertyName("column_name")]
    public string ColumnName { get; set; } = "";

    [JsonPropertyName("universe")]
    public string Universe { get; set; } = "";

    [JsonPropertyName("data_type")]
    public string DataType { get; set; } = "String";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("linked_ids")]
    public List<string> LinkedIds { get; set; } = new();

    [JsonPropertyName("role")]
    public string Role { get; set; } = "matched";

    [JsonPropertyName("allowed_values")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedValues { get; set; }

    [JsonPropertyName("score")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Score { get; set; }
}

