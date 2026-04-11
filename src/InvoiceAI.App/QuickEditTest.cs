// 快速测试 InvoiceDetailViewModel 的编辑保存功能
// 运行: dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -f net9.0-windows10.0.19041.0 -- --test --case=editsave

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;

namespace InvoiceAI.App;

public static class QuickEditTest
{
    public static async Task RunAsync(IServiceProvider services)
    {
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║  快速测试: ViewModel 编辑保存功能         ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine();

        var invoiceService = services.GetRequiredService<IInvoiceService>();
        var settingsService = services.GetRequiredService<IAppSettingsService>();

        // 1. 创建测试发票
        Console.WriteLine("1. 创建测试发票...");
        var testInvoice = new Invoice
        {
            IssuerName = "テスト会社",
            RegistrationNumber = "T1234567890123",
            TransactionDate = new DateTime(2026, 4, 1),
            Description = "原始内容",
            Category = "食料品",
            TaxExcludedAmount = 1000,
            TaxIncludedAmount = 1100,
            TaxAmount = 100,
            RecipientName = "交付先テスト",
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
        Console.WriteLine($"   ✓ 发票 ID={saved.Id}, IsConfirmed={saved.IsConfirmed}");
        Console.WriteLine();

        // 2. 创建 ViewModel
        Console.WriteLine("2. 创建 InvoiceDetailViewModel...");
        var vm = new InvoiceDetailViewModel(invoiceService, settingsService);
        vm.CurrentInvoice = saved;
        Console.WriteLine($"   ✓ CurrentInvoice 已设置");
        Console.WriteLine($"   ✓ IsEditMode={vm.IsEditMode} (期望: False)");
        Console.WriteLine($"   ✓ EditIssuerName=\"{vm.EditIssuerName}\"");
        Console.WriteLine($"   ✓ EditDescription=\"{vm.EditDescription}\"");
        Console.WriteLine();

        // 3. 进入编辑模式
        Console.WriteLine("3. 进入编辑模式...");
        vm.StartEditingCommand.Execute(null);
        Console.WriteLine($"   ✓ IsEditMode={vm.IsEditMode} (期望: True)");
        Console.WriteLine();

        // 4. 修改字段
        Console.WriteLine("4. 修改发票字段...");
        vm.EditIssuerName = "修正済み会社";
        vm.EditDescription = "修正后的内容";
        vm.EditCategory = "交通費";
        vm.EditTaxIncludedAmount = 1200;
        vm.EditRecipientName = "新しい交付先";

        if (vm.InvoiceItems.Count > 0)
        {
            vm.InvoiceItems[0].Name = "修正品目1";
            vm.InvoiceItems[0].Amount = 550;
        }
        Console.WriteLine($"   ✓ IssuerName → \"{vm.EditIssuerName}\"");
        Console.WriteLine($"   ✓ Description → \"{vm.EditDescription}\"");
        Console.WriteLine($"   ✓ Category → \"{vm.EditCategory}\"");
        Console.WriteLine($"   ✓ TaxIncludedAmount → {vm.EditTaxIncludedAmount}");
        Console.WriteLine($"   ✓ Items[0].Name → \"{vm.InvoiceItems[0].Name}\"");
        Console.WriteLine();

        // 5. 保存
        Console.WriteLine("5. 执行保存命令...");
        await vm.SaveCommand.ExecuteAsync(null);
        Console.WriteLine($"   ✓ IsEditMode={vm.IsEditMode} (期望: False)");
        Console.WriteLine($"   ✓ ValidationError=\"{vm.ValidationError}\"");
        Console.WriteLine();

        // 6. 验证数据库
        Console.WriteLine("6. 验证数据库...");
        var reloaded = await invoiceService.GetByIdAsync(saved.Id);
        if (reloaded == null)
        {
            Console.WriteLine("   ✗ 数据库中找不到发票记录!");
        }
        else
        {
            bool allPassed = true;

            Console.WriteLine($"   IssuerName: \"{reloaded.IssuerName}\" (期望: \"修正済み会社\") {(reloaded.IssuerName == "修正済み会社" ? "✓" : "✗")}");
            Console.WriteLine($"   Description: \"{reloaded.Description}\" (期望: \"修正后的内容\") {(reloaded.Description == "修正后的内容" ? "✓" : "✗")}");
            Console.WriteLine($"   Category: \"{reloaded.Category}\" (期望: \"交通費\") {(reloaded.Category == "交通費" ? "✓" : "✗")}");
            Console.WriteLine($"   TaxIncludedAmount: {reloaded.TaxIncludedAmount} (期望: 1200) {(reloaded.TaxIncludedAmount == 1200 ? "✓" : "✗")}");
            Console.WriteLine($"   RecipientName: \"{reloaded.RecipientName}\" (期望: \"新しい交付先\") {(reloaded.RecipientName == "新しい交付先" ? "✓" : "✗")}");
            Console.WriteLine($"   IsConfirmed: {reloaded.IsConfirmed} (期望: True) {(reloaded.IsConfirmed ? "✓" : "✗")}");

            var items = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<InvoiceItem>>(reloaded.ItemsJson ?? "[]");
            if (items != null && items.Count > 0)
            {
                Console.WriteLine($"   Items[0].Name: \"{items[0].Name}\" (期望: \"修正品目1\") {(items[0].Name == "修正品目1" ? "✓" : "✗")}");
                Console.WriteLine($"   Items[0].Amount: {items[0].Amount} (期望: 550) {(items[0].Amount == 550 ? "✓" : "✗")}");
            }
            Console.WriteLine();

            // 7. 验证验证功能
            Console.WriteLine("7. 测试验证: 空 IssuerName...");
            vm.CurrentInvoice = new Invoice { IssuerName = "", ItemsJson = "[]", IsConfirmed = false };
            vm.StartEditingCommand.Execute(null);
            await vm.SaveCommand.ExecuteAsync(null);
            Console.WriteLine($"   ✓ ValidationError=\"{vm.ValidationError}\" (应有错误消息)");
            Console.WriteLine();
        }

        // 8. 清理
        Console.WriteLine("8. 清理测试数据...");
        await invoiceService.DeleteAsync(saved.Id);
        Console.WriteLine("   ✓ 测试发票已删除");
        Console.WriteLine();

        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║          测试完成 ✓                       ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
    }
}
