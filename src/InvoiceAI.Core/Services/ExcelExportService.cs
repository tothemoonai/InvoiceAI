using InvoiceAI.Models;
using OfficeOpenXml;

namespace InvoiceAI.Core.Services;

public class ExcelExportService : IExcelExportService
{
    public ExcelExportService()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
#pragma warning restore CS0618
    }

    public async Task<string> ExportAsync(List<Invoice> invoices, string filePath)
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("发票导出");

        var headers = new[] { "日期", "発行事業者", "登録番号", "内容", "分类", "税抜金額", "税込金額", "消費税額", "适格状态", "缺失项" };
        for (int i = 0; i < headers.Length; i++)
            sheet.Cells[1, i + 1].Value = headers[i];

        using (var range = sheet.Cells[1, 1, 1, headers.Length])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        for (int r = 0; r < invoices.Count; r++)
        {
            var inv = invoices[r];
            var row = r + 2;
            sheet.Cells[row, 1].Value = inv.TransactionDate?.ToString("yyyy-MM-dd") ?? "";
            sheet.Cells[row, 2].Value = inv.IssuerName;
            sheet.Cells[row, 3].Value = inv.RegistrationNumber;
            sheet.Cells[row, 4].Value = inv.Description;
            sheet.Cells[row, 5].Value = inv.Category;
            sheet.Cells[row, 6].Value = (double?)inv.TaxExcludedAmount;
            sheet.Cells[row, 7].Value = (double?)inv.TaxIncludedAmount;
            sheet.Cells[row, 8].Value = (double?)inv.TaxAmount;
            sheet.Cells[row, 9].Value = inv.InvoiceType switch
            {
                InvoiceType.Standard => "標準",
                InvoiceType.Simplified => "簡易",
                _ => "非适格"
            };
            sheet.Cells[row, 10].Value = inv.MissingFields;
        }

        var summaryRow = invoices.Count + 2;
        sheet.Cells[summaryRow, 5].Value = "合计";
        sheet.Cells[summaryRow, 5].Style.Font.Bold = true;
        sheet.Cells[summaryRow, 6].Formula = $"SUM(F2:F{summaryRow - 1})";
        sheet.Cells[summaryRow, 7].Formula = $"SUM(G2:G{summaryRow - 1})";
        sheet.Cells[summaryRow, 8].Formula = $"SUM(H2:H{summaryRow - 1})";

        sheet.Cells.AutoFitColumns();

        var dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        await package.SaveAsAsync(new FileInfo(filePath));

        return filePath;
    }
}
