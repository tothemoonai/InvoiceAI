using InvoiceAI.Models;
using MiniExcelLibs;

namespace InvoiceAI.Core.Services;

public class ExcelExportService : IExcelExportService
{
    public async Task<string> ExportAsync(List<Invoice> invoices, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);

        var rows = new List<Dictionary<string, object?>>();

        // Header row
        foreach (var inv in invoices)
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
            ["税抜金額"] = invoices.Sum(i => i.TaxExcludedAmount ?? 0),
            ["税込金額"] = invoices.Sum(i => i.TaxIncludedAmount ?? 0),
            ["消費税額"] = invoices.Sum(i => i.TaxAmount ?? 0),
            ["适格状态"] = "",
            ["缺失项"] = ""
        });

        await MiniExcel.SaveAsAsync(filePath, rows);
        return filePath;
    }
}
