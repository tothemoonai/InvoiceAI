using System;
using System.IO;
using System.Threading.Tasks;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.App;

/// <summary>
/// 数据库诊断工具: 在 App.cs 的 OnStart 中调用以诊断数据问题
/// 用法: 在 App.cs 的 OnStart 中临时添加: await DbDiagnostic.RunAsync();
/// </summary>
public static class DbDiagnostic
{
    public static async Task RunAsync(AppDbContext db)
    {
        Console.WriteLine("=== 数据库诊断开始 ===");

        var total = await db.Invoices.CountAsync();
        var confirmed = await db.Invoices.CountAsync(i => i.IsConfirmed);
        var unconfirmed = await db.Invoices.CountAsync(i => !i.IsConfirmed);

        Console.WriteLine($"总记录数: {total}");
        Console.WriteLine($"已确认 (IsConfirmed=true): {confirmed}");
        Console.WriteLine($"未确认 (IsConfirmed=false): {unconfirmed}");
        Console.WriteLine($"未分类记录: {total - confirmed - unconfirmed}");

        if (total != confirmed + unconfirmed)
        {
            Console.WriteLine("\n⚠ 检测到 {0} 条 IsConfirmed 可能为 NULL 的记录", total - confirmed - unconfirmed);
            Console.WriteLine("   (EF Core 的 Where(!i.IsConfirmed) 不会匹配 NULL 值)");
            Console.WriteLine("\n前 10 条记录详情:");
            Console.WriteLine(new string('-', 90));
            Console.WriteLine($"{"Id",-5} {"IssuerName",-25} {"IsConfirmed",-12} {"Category",-15} {"TransactionDate",-15}");
            Console.WriteLine(new string('-', 90));

            var all = await db.Invoices.OrderBy(i => i.Id).Take(15).ToListAsync();
            foreach (var inv in all)
            {
                var name = inv.IssuerName?.Length > 20 ? inv.IssuerName.Substring(0, 20) + "..." : inv.IssuerName;
                var cat = inv.Category?.Length > 12 ? inv.Category.Substring(0, 12) + "..." : inv.Category;
                Console.WriteLine($"{inv.Id,-5} {(name ?? "null"),-28} {inv.IsConfirmed,-12} {(cat ?? "null"),-18} {inv.TransactionDate?.ToString("yyyy-MM-dd") ?? "null",-15}");
            }
        }

        // 分类统计
        var cats = await db.Invoices.GroupBy(i => i.Category)
            .Select(g => new { Cat = g.Key, Count = g.Count() })
            .ToListAsync();
        Console.WriteLine("\n分类统计:");
        foreach (var c in cats.OrderByDescending(x => x.Count))
            Console.WriteLine($"  {c.Cat ?? "(null)"}: {c.Count}");

        Console.WriteLine("\n=== 诊断结束 ===\n");
    }
}
