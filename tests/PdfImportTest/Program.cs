// PDF Import Test - Using APP's exact logic via InvoiceImportService
// Run: cd tests/PdfImportTest && dotnet run

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;
using InvoiceAI.Data;
using InvoiceAI.Core.Helpers;
using Microsoft.EntityFrameworkCore;

string FindProjectRoot()
{
    var dir = AppContext.BaseDirectory;
    for (int i = 0; i < 15; i++)
    {
        var tempDir = Path.Combine(dir, "TEMP");
        if (Directory.Exists(Path.Combine(tempDir, "testdata")) && Directory.Exists(Path.Combine(tempDir, "testlog")))
            return dir;
        var parent = Path.GetDirectoryName(dir);
        if (string.IsNullOrEmpty(parent) || parent == dir) break;
        dir = parent;
    }
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
}

var projectRoot = FindProjectRoot();
var testDataDir = Path.Combine(projectRoot, "TEMP", "testdata");
var pdfPath = Directory.GetFiles(testDataDir, "*饮食*.pdf").FirstOrDefault() 
           ?? Directory.GetFiles(testDataDir, "*.pdf").FirstOrDefault();

Console.WriteLine("PDF Import Test (Using InvoiceImportService)");
Console.WriteLine($"PDF: {pdfPath ?? "Not found"}");
Console.WriteLine();

if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath))
{
    Console.WriteLine("Error: PDF not found.");
    Environment.Exit(1);
}

// Setup DI exactly like MauiProgram.cs
var services = new ServiceCollection();
var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var dbPath = Path.Combine(localAppData, "User Name", "com.companyname.invoiceai.app", "Data", "invoices.db");
Console.WriteLine($"DB: {dbPath}");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

var settingsService = new AppSettingsService();
await settingsService.LoadAsync();

services.AddSingleton<IAppSettingsService>(settingsService);
services.AddHttpClient();
services.AddSingleton<IFileService, FileService>();
services.AddSingleton<IBaiduOcrService, BaiduOcrService>();
services.AddSingleton<IGlmService, GlmService>();
services.AddSingleton<IInvoiceService, InvoiceService>();
services.AddSingleton<IInvoiceImportService, InvoiceImportService>(); // THE KEY: Using the same service

var serviceProvider = services.BuildServiceProvider();
var importService = serviceProvider.GetRequiredService<IInvoiceImportService>();
var invoiceService = serviceProvider.GetRequiredService<IInvoiceService>();

await using var scope = serviceProvider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.EnsureCreatedAsync();

// Clear DB
Console.WriteLine("Clearing DB...");
var existingCount = db.Invoices.Count();
if (existingCount > 0)
{
    db.Invoices.RemoveRange(db.Invoices);
    await db.SaveChangesAsync();
    Console.WriteLine($"Deleted {existingCount} invoices.");
}

// Run Import
Console.WriteLine("\nStarting Import...");
var result = await importService.ImportAsync(new[] { pdfPath }, (batch, total) => 
{
    Console.WriteLine($"  Processing Batch {batch}/{total}...");
});

Console.WriteLine($"\n=== Results ===");
Console.WriteLine($"Total Images: {result.TotalImages}");
Console.WriteLine($"Success: {result.SuccessCount}");
Console.WriteLine($"Failed: {result.FailedCount}");

if (result.SuccessCount > 0)
{
    Console.WriteLine("\nSaved Invoices:");
    foreach (var inv in result.Invoices)
    {
        Console.WriteLine($"  - {inv.IssuerName} | {inv.Category} | Source: {Path.GetFileName(inv.SourceFilePath)}");
    }
    Console.WriteLine("\nTEST PASSED");
    Environment.Exit(0);
}
else
{
    Console.WriteLine("TEST FAILED");
    Environment.Exit(1);
}
