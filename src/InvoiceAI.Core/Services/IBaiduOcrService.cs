namespace InvoiceAI.Core.Services;

public interface IBaiduOcrService
{
    Task<string> RecognizeAsync(string filePath);
}
