using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceAI.Models;

/// <summary>
/// Converts JSON values to strings for deserialization.
/// Handles cases where LLM returns numbers instead of strings in arrays (e.g., missingFields: [1, 2]).
/// </summary>
public class FlexibleStringListConverter : JsonConverter<List<string>>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) return list;
                list.Add(reader.TokenType switch
                {
                    JsonTokenType.String => reader.GetString() ?? "",
                    JsonTokenType.Number => reader.GetInt32().ToString(),
                    JsonTokenType.True => "true",
                    JsonTokenType.False => "false",
                    JsonTokenType.Null => "",
                    _ => reader.GetString() ?? ""
                });
            }
            return list;
        }
        return [];
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>
/// Converts any JSON value (number, string, bool) to string.
/// Handles LLM inconsistencies: "10%", 10, "1,000", 1000.5 → all become strings.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "",
            JsonTokenType.Number => reader.GetDouble().ToString(),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => "",
            _ => reader.GetString() ?? ""
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

public class GlmInvoiceResponse
{
    [JsonPropertyName("issuerName")]
    public string IssuerName { get; set; } = string.Empty;

    [JsonPropertyName("registrationNumber")]
    public string RegistrationNumber { get; set; } = string.Empty;

    [JsonPropertyName("transactionDate")]
    public string? TransactionDate { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<GlmInvoiceItem> Items { get; set; } = [];

    [JsonPropertyName("taxExcludedAmount")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string TaxExcludedAmount { get; set; } = string.Empty;

    [JsonPropertyName("taxIncludedAmount")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string TaxIncludedAmount { get; set; } = string.Empty;

    [JsonPropertyName("taxAmount")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string TaxAmount { get; set; } = string.Empty;

    [JsonPropertyName("recipientName")]
    public string? RecipientName { get; set; }

    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; set; } = "NonQualified";

    [JsonPropertyName("missingFields")]
    [JsonConverter(typeof(FlexibleStringListConverter))]
    public List<string> MissingFields { get; set; } = [];

    [JsonPropertyName("suggestedCategory")]
    public string SuggestedCategory { get; set; } = "その他";

    // Token usage (not from JSON, set by GlmService)
    [JsonIgnore]
    public int PromptTokens { get; set; }

    [JsonIgnore]
    public int CompletionTokens { get; set; }

    [JsonIgnore]
    public int TotalTokens { get; set; }
}

public class GlmInvoiceItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Amount { get; set; } = string.Empty;

    [JsonPropertyName("taxRate")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string TaxRate { get; set; } = string.Empty;

    [JsonPropertyName("isReducedRate")]
    public bool IsReducedRate { get; set; }
}
