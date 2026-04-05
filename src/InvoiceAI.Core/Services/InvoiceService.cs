using InvoiceAI.Data;
using InvoiceAI.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Core.Services;

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _dbContext;

    public InvoiceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Invoice>> GetAllAsync()
        => await _dbContext.Invoices.OrderByDescending(i => i.TransactionDate).ToListAsync();

    public async Task<List<Invoice>> GetByCategoryAsync(string category)
        => await _dbContext.Invoices
            .Where(i => i.Category == category)
            .OrderByDescending(i => i.TransactionDate)
            .ToListAsync();

    public async Task<List<Invoice>> GetByDateRangeAsync(DateTime start, DateTime end)
        => await _dbContext.Invoices
            .Where(i => i.TransactionDate >= start && i.TransactionDate <= end)
            .OrderByDescending(i => i.TransactionDate)
            .ToListAsync();

    public async Task<List<Invoice>> GetUnconfirmedAsync()
        => await _dbContext.Invoices
            .Where(i => !i.IsConfirmed)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

    public async Task<Invoice?> GetByIdAsync(int id)
        => await _dbContext.Invoices.FindAsync(id);

    public async Task<Invoice> SaveAsync(Invoice invoice)
    {
        invoice.CreatedAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();
        return invoice;
    }

    public async Task UpdateAsync(Invoice invoice)
    {
        invoice.UpdatedAt = DateTime.UtcNow;
        _dbContext.Invoices.Update(invoice);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var invoice = await _dbContext.Invoices.FindAsync(id);
        if (invoice != null)
        {
            _dbContext.Invoices.Remove(invoice);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByHashAsync(string fileHash)
        => await _dbContext.Invoices.AnyAsync(i => i.FileHash == fileHash);

    public async Task<Dictionary<string, int>> GetCategoryCountsAsync()
        => await _dbContext.Invoices
            .GroupBy(i => i.Category)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
}
