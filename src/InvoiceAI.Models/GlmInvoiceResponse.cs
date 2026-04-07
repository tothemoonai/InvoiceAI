using System.Text.Json.Serialization;

namespace InvoiceAI.Models;

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
    public decimal? TaxExcludedAmount { get; set; }

    [JsonPropertyName("taxIncludedAmount")]
    public decimal? TaxIncludedAmount { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal? TaxAmount { get; set; }

    [JsonPropertyName("recipientName")]
    public string? RecipientName { get; set; }

    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; set; } = "NonQualified";

    [JsonPropertyName("missingFields")]
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
    public decimal Amount { get; set; }

    [JsonPropertyName("taxRate")]
    public int TaxRate { get; set; }

    [JsonPropertyName("isReducedRate")]
    public bool IsReducedRate { get; set; }
}