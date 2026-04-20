using System.ComponentModel.DataAnnotations.Schema;

namespace InvoiceAI.Models;

public class Invoice
{
    public int Id { get; set; }
    public string IssuerName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime? TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]"; // JSON array of InvoiceItem
    public decimal? TaxExcludedAmount { get; set; }
    public decimal? TaxIncludedAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string? RecipientName { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public string? MissingFields { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SourceFilePath { get; set; }
    public string? FileHash { get; set; }
    public string? OcrRawText { get; set; }
    public string? GlmRawResponse { get; set; }
    public bool IsConfirmed { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    [NotMapped]
    public bool IsSelected { get; set; } // 用于UI多选，不存储到数据库

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}