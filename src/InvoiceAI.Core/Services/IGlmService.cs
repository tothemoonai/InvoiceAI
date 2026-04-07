using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public interface IGlmService
{
    Task<GlmInvoiceResponse> ProcessInvoiceAsync(string ocrText);
    Task<List<GlmInvoiceResponse>> ProcessBatchAsync(string[] ocrTexts);
}
