namespace InvoiceAI.Models;

public class InvoiceItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TaxRate { get; set; }
    public bool IsReducedRate { get; set; }
}