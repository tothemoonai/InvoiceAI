using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public interface IExcelExportService
{
    Task<string> ExportAsync(List<Invoice> invoices, string filePath);
}
