using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public interface IGlmService
{
    event EventHandler<string>? StatusChanged;
    Task<GlmInvoiceResponse> ProcessInvoiceAsync(string ocrText);
    Task<List<GlmInvoiceResponse>> ProcessBatchAsync(string[] ocrTexts);
}
