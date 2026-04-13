// 独立测试 InvoiceDetailViewModel 编辑保存功能
// 运行: cd tests/EditFunctionTest && dotnet run

using System;
using System.Threading.Tasks;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║  编辑保存功能测试                         ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();

// 构建服务
var services = BuildServices();

var invoiceService = services.GetRequiredService<IInvoiceService>();
var settingsService = services.GetRequiredService<IAppSettingsService>();

// 确保数据库初始化
await using var scope = services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.EnsureCreatedAsync();

int passed = 0;
int failed = 0;

// 测试 1: 基本编辑保存流程
Console.WriteLine("测试 1: 基本编辑保存流程");
Console.WriteLine("-".Repeat(50));
try
{
    var testInvoice = new Invoice
    {
        IssuerName = "テスト会社",
        Description = "原始内容",
        Category = "食料品",
        TaxIncludedAmount = 1100,
        InvoiceType = InvoiceType.Standard,
        ItemsJson = System.Text.Json.JsonSerializer.Serialize(new System.Collections.Generic.List<InvoiceItem>
        {
            new() { Name = "品目1", Amount = 500, TaxRate = 10 },
            new() { Name = "品目2", Amount = 600, TaxRate = 10 }
        }),
        MissingFields = "[]",
        IsConfirmed = false
    };

    var saved = await invoiceService.SaveAsync(testInvoice);
    Console.WriteLine($"  ✓ 创建发票 ID={saved.Id}");

    var vm = new InvoiceDetailViewModel(invoiceService, settingsService);
    vm.CurrentInvoice = saved;

    if (vm.IsEditMode)
    {
        Console.WriteLine($"  ✗ 失败: 初始 IsEditMode 应为 false");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 初始 IsEditMode=false");
        passed++;
    }

    vm.StartEditingCommand.Execute(null);
    if (!vm.IsEditMode)
    {
        Console.WriteLine($"  ✗ 失败: StartEditing 后 IsEditMode 应为 true");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 进入编辑模式 IsEditMode=true");
        passed++;
    }

    // 修改字段
    vm.EditIssuerName = "修正済み会社";
    vm.EditDescription = "修正后的内容";
    vm.EditCategory = "交通費";
    vm.EditTaxIncludedAmount = 1200;

    if (vm.InvoiceItems.Count > 0)
    {
        vm.InvoiceItems[0].Name = "修正品目1";
        vm.InvoiceItems[0].Amount = 550;
    }
    Console.WriteLine($"  ✓ 修改编辑字段");

    await vm.SaveCommand.ExecuteAsync(null);

    if (vm.IsEditMode)
    {
        Console.WriteLine($"  ✗ 失败: 保存后 IsEditMode 应为 false");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 保存后退出编辑模式 IsEditMode=false");
        passed++;
    }

    if (!string.IsNullOrEmpty(vm.ValidationError))
    {
        Console.WriteLine($"  ✗ 失败: 保存后不应有验证错误: {vm.ValidationError}");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 无验证错误");
        passed++;
    }

    // 验证数据库（注意：编辑保存不改变 IsConfirmed 和 Category）
    var reloaded = await invoiceService.GetByIdAsync(saved.Id);
    bool dbPassed = reloaded != null
        && reloaded.IssuerName == "修正済み会社"
        && reloaded.Description == "修正后的内容"
        && reloaded.Category == "交通費"  // 编辑时修改了 Category
        && reloaded.TaxIncludedAmount == 1200
        && !reloaded.IsConfirmed;  // 编辑保存不应改变 IsConfirmed

    if (!dbPassed)
    {
        Console.WriteLine($"  ✗ 失败: 数据库值不匹配");
        Console.WriteLine($"    IssuerName: {reloaded?.IssuerName}");
        Console.WriteLine($"    Description: {reloaded?.Description}");
        Console.WriteLine($"    Category: {reloaded?.Category}");
        Console.WriteLine($"    Amount: {reloaded?.TaxIncludedAmount}");
        Console.WriteLine($"    IsConfirmed: {reloaded?.IsConfirmed} (期望: False)");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 数据库值正确更新");
        Console.WriteLine($"    IssuerName: {reloaded.IssuerName}");
        Console.WriteLine($"    Description: {reloaded.Description}");
        Console.WriteLine($"    Category: {reloaded.Category}");
        Console.WriteLine($"    Amount: {reloaded.TaxIncludedAmount}");
        Console.WriteLine($"    IsConfirmed: {reloaded.IsConfirmed} (未改变，仍为 False)");
        passed++;
    }

    // 验证 ItemsJson
    var items = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<InvoiceItem>>(reloaded?.ItemsJson ?? "[]");
    if (items != null && items.Count > 0 && items[0].Name == "修正品目1" && items[0].Amount == 550)
    {
        Console.WriteLine($"  ✓ 明细项目正确保存");
        passed++;
    }
    else
    {
        Console.WriteLine($"  ✗ 失败: 明细项目未正确保存");
        failed++;
    }

    // 清理
    await invoiceService.DeleteAsync(saved.Id);
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"  ✗ 异常: {ex.Message}");
    failed++;
    Console.WriteLine();
}

// 测试 2: 验证空 IssuerName
Console.WriteLine("测试 2: 验证空 IssuerName 阻止保存");
Console.WriteLine("-".Repeat(50));
try
{
    var testInvoice = new Invoice
    {
        IssuerName = "",
        Description = "测试验证",
        Category = "测试",
        IsConfirmed = false,
        ItemsJson = "[]"
    };

    var saved = await invoiceService.SaveAsync(testInvoice);
    var vm = new InvoiceDetailViewModel(invoiceService, settingsService);
    vm.CurrentInvoice = saved;
    vm.StartEditingCommand.Execute(null);

    await vm.SaveCommand.ExecuteAsync(null);

    if (string.IsNullOrEmpty(vm.ValidationError))
    {
        Console.WriteLine($"  ✗ 失败: 空 IssuerName 应触发验证错误");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 验证错误: {vm.ValidationError}");
        passed++;
    }

    await invoiceService.DeleteAsync(saved.Id);
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"  ✗ 异常: {ex.Message}");
    failed++;
    Console.WriteLine();
}

// 测试 3: 取消编辑
Console.WriteLine("测试 3: 取消编辑恢复原值");
Console.WriteLine("-".Repeat(50));
try
{
    var testInvoice = new Invoice
    {
        IssuerName = "原始公司",
        Description = "原始描述",
        Category = "原始分类",
        IsConfirmed = false,
        ItemsJson = "[]"
    };

    var saved = await invoiceService.SaveAsync(testInvoice);
    var vm = new InvoiceDetailViewModel(invoiceService, settingsService);
    vm.CurrentInvoice = saved;
    vm.StartEditingCommand.Execute(null);

    // 修改
    vm.EditIssuerName = "修改后";
    vm.CancelEditingCommand.Execute(null);

    if (vm.EditIssuerName != "原始公司")
    {
        Console.WriteLine($"  ✗ 失败: 取消编辑后应恢复原值");
        Console.WriteLine($"    期望: 原始公司, 实际: {vm.EditIssuerName}");
        failed++;
    }
    else
    {
        Console.WriteLine($"  ✓ 取消编辑后恢复原值: {vm.EditIssuerName}");
        passed++;
    }

    await invoiceService.DeleteAsync(saved.Id);
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"  ✗ 异常: {ex.Message}");
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
