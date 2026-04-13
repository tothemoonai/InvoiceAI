// 测试已导出发票详情对话框的数据加载和编辑功能
// 运行: cd tests/ExportedInvoiceDetailTest && dotnet run

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║  已导出发票详情对话框测试                ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();

var services = BuildServices();
var invoiceService = services.GetRequiredService<IInvoiceService>();
var settingsService = services.GetRequiredService<IAppSettingsService>();

await using var scope = services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.EnsureCreatedAsync();

int passed = 0;
int failed = 0;

// 测试 1: 模拟已导出发票详情对话框的数据加载
Console.WriteLine("测试 1: 已导出发票详情数据加载（模拟 SavedInvoiceDetailDialog 打开）");
Console.WriteLine("-".Repeat(50));
try
{
    // 创建已导出发票（模拟从数据库获取）
    var testInvoice = new Invoice
    {
        IssuerName = "テスト会社株式会社",
        RegistrationNumber = "T1234567890123",
        TransactionDate = new DateTime(2026, 3, 15),
        Description = "オフィス用品購入",
        Category = "事務用品",
        TaxExcludedAmount = 10000,
        TaxIncludedAmount = 11000,
        TaxAmount = 1000,
        RecipientName = "自社名前",
        InvoiceType = InvoiceType.Standard,
        ItemsJson = JsonSerializer.Serialize(new List<InvoiceItem>
        {
            new() { Name = "コピー用紙", Amount = 3000, TaxRate = 10 },
            new() { Name = "インクカートリッジ", Amount = 5000, TaxRate = 10 },
            new() { Name = "フォルダー", Amount = 3000, TaxRate = 10 }
        }),
        MissingFields = "[]",
        IsConfirmed = true  // 已导出
    };

    var saved = await invoiceService.SaveAsync(testInvoice);
    Console.WriteLine($"  ✓ 创建已导出发票 ID={saved.Id}, IsConfirmed={saved.IsConfirmed}");

    // 模拟 SavedInvoiceDetailDialog.LoadInvoiceData()
    var loadedInvoice = await invoiceService.GetByIdAsync(saved.Id);
    
    if (loadedInvoice == null)
    {
        Console.WriteLine($"  ✗ 失败: GetByIdAsync 返回 null");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 从数据库加载成功 ID={loadedInvoice.Id}");

        // 模拟对话框中各个字段的赋值（与 SavedInvoiceDetailDialog.BuildUI() 相同逻辑）
        var issuerText = loadedInvoice.IssuerName ?? string.Empty;
        var regNumberText = loadedInvoice.RegistrationNumber ?? string.Empty;
        var dateValue = loadedInvoice.TransactionDate ?? DateTime.Now;
        var descriptionText = loadedInvoice.Description ?? string.Empty;
        var categoryText = loadedInvoice.Category ?? string.Empty;
        var exclAmountText = loadedInvoice.TaxExcludedAmount?.ToString() ?? "";
        var inclAmountText = loadedInvoice.TaxIncludedAmount?.ToString() ?? "";
        var taxAmountText = loadedInvoice.TaxAmount?.ToString() ?? "";
        var recipientText = loadedInvoice.RecipientName ?? string.Empty;

        bool dataLoaded = issuerText == "テスト会社株式会社"
            && regNumberText == "T1234567890123"
            && dateValue == new DateTime(2026, 3, 15)
            && descriptionText == "オフィス用品購入"
            && categoryText == "事務用品"
            && exclAmountText == "10000"
            && inclAmountText == "11000"
            && taxAmountText == "1000"
            && recipientText == "自社名前";

        if (!dataLoaded)
        {
            Console.WriteLine($"  ✗ 失败: 字段值不正确");
            Console.WriteLine($"    IssuerName: \"{issuerText}\" (期望: \"テスト会社株式会社\")");
            Console.WriteLine($"    RegistrationNumber: \"{regNumberText}\" (期望: \"T1234567890123\")");
            Console.WriteLine($"    TransactionDate: {dateValue:yyyy-MM-dd} (期望: 2026-03-15)");
            Console.WriteLine($"    Description: \"{descriptionText}\" (期望: \"オフィス用品購入\")");
            Console.WriteLine($"    Category: \"{categoryText}\" (期望: \"事務用品\")");
            Console.WriteLine($"    TaxExcludedAmount: \"{exclAmountText}\" (期望: \"10000\")");
            Console.WriteLine($"    TaxIncludedAmount: \"{inclAmountText}\" (期望: \"11000\")");
            Console.WriteLine($"    TaxAmount: \"{taxAmountText}\" (期望: \"1000\")");
            Console.WriteLine($"    RecipientName: \"{recipientText}\" (期望: \"自社名前\")");
            failed++;
        }
        else
        {
            Console.WriteLine($"  ✓ 所有字段值正确:");
            Console.WriteLine($"    IssuerName: {issuerText}");
            Console.WriteLine($"    RegistrationNumber: {regNumberText}");
            Console.WriteLine($"    TransactionDate: {dateValue:yyyy-MM-dd}");
            Console.WriteLine($"    Description: {descriptionText}");
            Console.WriteLine($"    Category: {categoryText}");
            Console.WriteLine($"    TaxExcludedAmount: {exclAmountText}");
            Console.WriteLine($"    TaxIncludedAmount: {inclAmountText}");
            Console.WriteLine($"    TaxAmount: {taxAmountText}");
            Console.WriteLine($"    RecipientName: {recipientText}");
            passed++;
        }

        // 模拟明细项目加载（与 SavedInvoiceDetailDialog 相同逻辑）
        try
        {
            var items = JsonSerializer.Deserialize<List<InvoiceItem>>(loadedInvoice.ItemsJson ?? "[]") ?? [];
            if (items.Count == 3 && items[0].Name == "コピー用紙" && items[0].Amount == 3000)
            {
                Console.WriteLine($"  ✓ 明细项目加载成功: {items.Count} 项");
                foreach (var item in items)
                {
                    Console.WriteLine($"    - {item.Name}: ¥{item.Amount}");
                }
                passed++;
            }
            else
            {
                Console.WriteLine($"  ✗ 失败: 明细项目数据不正确");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 失败: 明细项目解析异常: {ex.Message}");
            failed++;
        }
    }

    await invoiceService.DeleteAsync(saved.Id);
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"  ✗ 异常: {ex.Message}");
    Console.WriteLine($"    堆栈: {ex.StackTrace}");
    failed++;
    Console.WriteLine();
}

// 测试 2: 模拟已导出发票编辑保存
Console.WriteLine("测试 2: 已导出发票编辑保存（模拟 SavedInvoiceDetailDialog 保存）");
Console.WriteLine("-".Repeat(50));
try
{
    var testInvoice = new Invoice
    {
        IssuerName = "导出会社",
        RegistrationNumber = "T9999999999999",
        TransactionDate = new DateTime(2026, 1, 1),
        Description = "导出前内容",
        Category = "食料品",
        TaxExcludedAmount = 5000,
        TaxIncludedAmount = 5500,
        TaxAmount = 500,
        RecipientName = "客户A",
        InvoiceType = InvoiceType.Standard,
        ItemsJson = JsonSerializer.Serialize(new List<InvoiceItem>
        {
            new() { Name = "品目1", Amount = 3000, TaxRate = 10 },
            new() { Name = "品目2", Amount = 2500, TaxRate = 10 }
        }),
        IsConfirmed = true
    };

    var saved = await invoiceService.SaveAsync(testInvoice);
    Console.WriteLine($"  ✓ 创建已导出发票 ID={saved.Id}, IsConfirmed={saved.IsConfirmed}");

    // 模拟对话框加载
    var loaded = await invoiceService.GetByIdAsync(saved.Id);
    if (loaded == null)
    {
        Console.WriteLine($"  ✗ 失败: 加载发票失败");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 对话框加载成功");

        // 模拟用户编辑（与 SavedInvoiceDetailDialog.OnSaveClicked() 相同逻辑）
        loaded.IssuerName = "修正会社";
        loaded.RegistrationNumber = "T8888888888888";
        loaded.TransactionDate = new DateTime(2026, 4, 1);
        loaded.Description = "修正后内容";
        loaded.Category = "交通費";
        loaded.TaxExcludedAmount = 8000;
        loaded.TaxIncludedAmount = 8800;
        loaded.TaxAmount = 800;
        loaded.RecipientName = "客户B";
        loaded.InvoiceType = InvoiceType.Simplified;
        loaded.ItemsJson = JsonSerializer.Serialize(new List<InvoiceItem>
        {
            new() { Name = "修正品目A", Amount = 5000, TaxRate = 10 },
            new() { Name = "修正品目B", Amount = 3800, TaxRate = 10 }
        });
        loaded.UpdatedAt = DateTime.UtcNow;
        // 注意：不改变 IsConfirmed

        await invoiceService.UpdateAsync(loaded);
        Console.WriteLine($"  ✓ 执行更新");

        // 验证数据库
        var reloaded = await invoiceService.GetByIdAsync(saved.Id);
        bool dbPassed = reloaded != null
            && reloaded.IssuerName == "修正会社"
            && reloaded.RegistrationNumber == "T8888888888888"
            && reloaded.TransactionDate == new DateTime(2026, 4, 1)
            && reloaded.Description == "修正后内容"
            && reloaded.Category == "交通費"
            && reloaded.TaxExcludedAmount == 8000
            && reloaded.TaxIncludedAmount == 8800
            && reloaded.TaxAmount == 800
            && reloaded.RecipientName == "客户B"
            && reloaded.InvoiceType == InvoiceType.Simplified
            && reloaded.IsConfirmed;  // 已导出发票编辑后仍应保持 IsConfirmed=true

        if (!dbPassed)
        {
            Console.WriteLine($"  ✗ 失败: 数据库值不匹配");
            Console.WriteLine($"    IssuerName: {reloaded?.IssuerName}");
            Console.WriteLine($"    IsConfirmed: {reloaded?.IsConfirmed} (期望: True)");
            failed++;
        }
        else
        {
            Console.WriteLine($"  ✓ 数据库值正确更新");
            Console.WriteLine($"    IssuerName: {reloaded.IssuerName}");
            Console.WriteLine($"    Description: {reloaded.Description}");
            Console.WriteLine($"    Category: {reloaded.Category}");
            Console.WriteLine($"    IsConfirmed: {reloaded.IsConfirmed} (保持 True)");
            passed++;
        }

        // 验证明细项目
        var items = JsonSerializer.Deserialize<List<InvoiceItem>>(reloaded?.ItemsJson ?? "[]");
        if (items != null && items.Count == 2 && items[0].Name == "修正品目A" && items[0].Amount == 5000)
        {
            Console.WriteLine($"  ✓ 明细项目正确保存: {items.Count} 项");
            passed++;
        }
        else
        {
            Console.WriteLine($"  ✗ 失败: 明细项目未正确保存");
            failed++;
        }
    }

    await invoiceService.DeleteAsync(saved.Id);
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"  ✗ 异常: {ex.Message}");
    Console.WriteLine($"    堆栈: {ex.StackTrace}");
    failed++;
    Console.WriteLine();
}

// 总结
Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine($"║  测试完成: {passed} 通过, {failed} 失败           ║");
Console.WriteLine("╚══════════════════════════════════════════╝");

Environment.Exit(failed > 0 ? 1 : 0);

static IServiceProvider BuildServices()
{
    var services = new ServiceCollection();

    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceAI", "invoices.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

    services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    services.AddScoped<IInvoiceService, InvoiceService>();
    services.AddSingleton<IAppSettingsService, AppSettingsService>();

    return services.BuildServiceProvider();
}

public static class StringExtensions
{
    public static string Repeat(this string s, int count) => new string(s[0], count);
}
