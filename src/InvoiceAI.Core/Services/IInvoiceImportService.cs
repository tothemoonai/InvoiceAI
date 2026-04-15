using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public interface IInvoiceImportService
{
    /// <summary>
    /// Raised when the import status changes (e.g., provider fallback).
    /// </summary>
    event EventHandler<string>? StatusChanged;

    /// <summary>
    /// 导入文件列表（支持图片和 PDF）
    /// </summary>
    /// <param name="filePaths">文件路径列表</param>
    /// <param name="onProgress">进度回调 (batch, totalBatches)</param>
    /// <returns>处理结果统计</returns>
    Task<ImportResult> ImportAsync(IEnumerable<string> filePaths, Action<int, int>? onProgress = null);
}

public class ImportResult
{
    public int TotalFiles { get; set; }
    public int TotalImages { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; } = new();
    public List<Invoice> Invoices { get; } = new();
}
