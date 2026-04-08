using InvoiceAI.Models;
using MiniExcelLibs;

namespace InvoiceAI.Core.Services;

public class ExcelExportService : IExcelExportService
{
    public async Task<string> ExportAsync(List<Invoice> invoices, string filePath, DateTime? startDate = null, DateTime? endDate = null)
    {
        var dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);

        // Filter by date range if provided
        var filteredInvoices = invoices;
        if (startDate.HasValue || endDate.HasValue)
        {
            filteredInvoices = invoices.Where(i =>
            {
                if (!i.TransactionDate.HasValue) return false;
                if (startDate.HasValue && i.TransactionDate.Value < startDate.Value) return false;
                if (endDate.HasValue && i.TransactionDate.Value > endDate.Value) return false;
                return true;
            }).ToList();
        }

        var rows = new List<Dictionary<string, object?>>();

        // Header row
        foreach (var inv in filteredInvoices)
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["日期"] = inv.TransactionDate?.ToString("yyyy-MM-dd") ?? "",
                ["発行事業者"] = inv.IssuerName,
                ["登録番号"] = inv.RegistrationNumber,
                ["内容"] = inv.Description,
                ["分类"] = inv.Category,
                ["税抜金額"] = inv.TaxExcludedAmount,
                ["税込金額"] = inv.TaxIncludedAmount,
                ["消費税額"] = inv.TaxAmount,
                ["适格状态"] = inv.InvoiceType switch
                {
                    InvoiceType.Standard => "標準",
                    InvoiceType.Simplified => "簡易",
                    _ => "非适格"
                },
                ["缺失项"] = inv.MissingFields
            });
        }

        // Summary row
        rows.Add(new Dictionary<string, object?>
        {
            ["日期"] = "",
            ["発行事業者"] = "",
            ["登録番号"] = "",
            ["内容"] = "",
            ["分类"] = "合计",
            ["税抜金額"] = filteredInvoices.Sum(i => i.TaxExcludedAmount ?? 0),
            ["税込金額"] = filteredInvoices.Sum(i => i.TaxIncludedAmount ?? 0),
            ["消費税額"] = filteredInvoices.Sum(i => i.TaxAmount ?? 0),
            ["适格状态"] = "",
            ["缺失项"] = ""
        });

        await MiniExcel.SaveAsAsync(filePath, rows);
        return filePath;
    }
}
