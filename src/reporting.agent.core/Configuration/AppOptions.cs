namespace reporting.agent.core.Configuration;

public sealed class DruidOptions
{
    public const string SectionName = "Druid";

    public string Host { get; set; } = "http://localhost:8082";
    public string AuthBase64 { get; set; } = "";
}

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Url { get; set; } = "http://localhost:6334";
    public string? ApiKey { get; set; }
    public string ReportingColumnsCollection { get; set; } = "reporting_columns";
}

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string EmbeddingsDeployment { get; set; } = "text-embedding-3-small";
    public string EmbeddingsApiVersion { get; set; } = "2024-02-15-preview";
}

public sealed class SecureGatewayOptions
{
    public const string SectionName = "SecureGateway";

    public string Url { get; set; } = "";
    public string ServiceKey { get; set; } = "";
    public string UserPermissionsFile { get; set; } = "Resources/secure-gateway/user_permissions.txt";
}

public enum FieldRetrievalMode
{
    Taxonomy,
    Vector,
}

public sealed class FieldRetrievalOptions
{
    public const string SectionName = "FieldRetrieval";

    public FieldRetrievalMode Mode { get; set; } = FieldRetrievalMode.Taxonomy;
    public int DefaultTopK { get; set; } = 20;
    public string ColumnsDirectory { get; set; } = "Resources/columns";
}

