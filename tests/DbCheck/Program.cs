// 检查数据库内容
using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using InvoiceAI.Data;
using InvoiceAI.Models;

string FindDatabasePath()
{
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    var possiblePaths = new[]
    {
        Path.Combine(localAppData, "User Name", "com.companyname.invoiceai.app", "Data", "invoices.db"),
        Path.Combine(localAppData, "com.companyname.invoiceai.app", "Data", "invoices.db"),
        Path.Combine(localAppData, "com.companyname.invoiceai.app", "invoices.db"),
        Path.Combine(roamingAppData, "InvoiceAI", "invoices.db"),
        Path.Combine(localAppData, "InvoiceAI", "invoices.db"),
        Path.Combine(localAppData, "invoices.db"),
    };

    Console.WriteLine("Checking database paths...");
    foreach (var p in possiblePaths)
    {
        if (File.Exists(p))
        {
            Console.WriteLine($"Found: {p} (Size: {new FileInfo(p).Length} bytes)");
            return p;
        }
    }
    return possiblePaths.Last();
}

var dbPath = FindDatabasePath();
Console.WriteLine($"\nUsing DB: {dbPath}");

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");

using var db = new AppDbContext(optionsBuilder.Options);

var allInvoices = db.Invoices.ToList();
var unconfirmed = db.Invoices.Where(i => !i.IsConfirmed).ToList();
var confirmed = db.Invoices.Where(i => i.IsConfirmed).ToList();

Console.WriteLine($"\nTotal Invoices: {allInvoices.Count}");
Console.WriteLine($"Unconfirmed (Main List): {unconfirmed.Count}");
Console.WriteLine($"Confirmed (Exported List): {confirmed.Count}");

if (allInvoices.Count > 0)
{
    Console.WriteLine("\nFirst 5 Invoices:");
    foreach (var inv in allInvoices.Take(5))
    {
        Console.WriteLine($"  ID={inv.Id}, Issuer={inv.IssuerName}, Category={inv.Category}, Confirmed={inv.IsConfirmed}");
    }
}
else
{
    Console.WriteLine("\nDatabase is EMPTY.");
}
