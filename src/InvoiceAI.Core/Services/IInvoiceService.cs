using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public interface IInvoiceService
{
    Task<List<Invoice>> GetAllAsync();
    Task<List<Invoice>> GetByCategoryAsync(string category);
    Task<List<Invoice>> GetByDateRangeAsync(DateTime start, DateTime end);
    Task<List<Invoice>> GetUnconfirmedAsync();
    Task<List<Invoice>> GetConfirmedAsync();
    Task<List<Invoice>> GetByCreateDateRangeAsync(DateTime start, DateTime end);
    List<string> GetDistinctCategories();
    Task<Invoice?> GetByIdAsync(int id);
    Task<Invoice> SaveAsync(Invoice invoice);
    Task UpdateAsync(Invoice invoice);
    Task DeleteAsync(int id);
    Task<bool> ExistsByHashAsync(string fileHash);
    Task<Dictionary<string, int>> GetCategoryCountsAsync();
}
