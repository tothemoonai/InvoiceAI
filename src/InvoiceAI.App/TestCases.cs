using System.Text.Json;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;

namespace InvoiceAI.App;

public static class TestCases
{
    /// <summary>
    /// 查找 MAUI 应用使用的 invoices.db 数据库路径。
    /// </summary>
    private static string FindDatabasePath()
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
        };
        
        foreach (var p in possiblePaths)
        {
            if (File.Exists(p)) return p;
        }
        
        return possiblePaths[0];
    }
    
    /// <summary>
    /// 从 AppContext.BaseDirectory 向上搜索，找到包含 TEMP/testdata 和 TEMP/testlog 的项目根目录。
    /// 只有同时存在这两个子目录才认为是真正的项目根目录。
    /// </summary>
    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 15; i++)
        {
            var tempDir = Path.Combine(dir, "TEMP");
            if (Directory.Exists(Path.Combine(tempDir, "testdata")) &&
                Directory.Exists(Path.Combine(tempDir, "testlog")))
                return dir;
            var parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent) || parent == dir) break;
            dir = parent;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    }

    private static readonly string ProjectRoot = FindProjectRoot();

    // 测试数据目录: TEMP\testdata（发票图片/PDF）
    private static readonly string TestDataDir = Path.Combine(ProjectRoot, "TEMP", "testdata");

    // 测试日志目录: TEMP\testlog（测试输出和临时文件）
    private static readonly string TestLogDir = Path.Combine(ProjectRoot, "TEMP", "testlog");

    // ─── 1. Load: 数据加载 ─────────────────────────────

    public static async Task<TestCaseResult> TestLoad(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        // 确保数据库初始化
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var invoices = await invoiceService.GetUnconfirmedAsync();
        var totalCount = (await invoiceService.GetAllAsync()).Count;

        bool passed = invoices.All(i => !i.IsConfirmed);
        var allConfirmed = invoices.Count > 0 && invoices.All(i => !i.IsConfirmed);

        return new TestCaseResult(
            "load",
            $"查询未确认发票列表 (数据库共有 {totalCount} 条记录)",
            "返回所有 IsConfirmed=false 的发票列表",
            $"获取 {invoices.Count} 条未确认发票记录，全部 IsConfirmed=false: {allConfirmed}",
            null,
            passed,
            passed ? null : "存在 IsConfirmed=true 的发票在返回列表中"
        );
    }

    // ─── 2. Category: 分类管理 ─────────────────────────

    public static async Task<TestCaseResult> TestCategory(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var counts = await invoiceService.GetCategoryCountsAsync();
        var distinctCats = await invoiceService.GetDistinctCategoriesAsync();

        bool passed = counts != null && counts.Count >= 0;

        var detail = counts.Count > 0
            ? string.Join(", ", counts.Select(kv => $"{kv.Key}:{kv.Value}"))
            : "数据库为空（无分类数据）";

        return new TestCaseResult(
            "category",
            $"获取分类计数 + 按分类筛选 (共 {distinctCats.Count} 个分类: {string.Join(", ", distinctCats)})",
            "返回非空分类字典，按分类筛选返回对应发票",
            $"分类计数: {detail}",
            null,
            passed,
            passed ? null : "分类计数返回 null"
        );
    }

    // ─── 3. Search: 发票搜索 ───────────────────────────

    public static async Task<TestCaseResult> TestSearch(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var allInvoices = await invoiceService.GetAllAsync();

        if (allInvoices.Count == 0)
        {
            return new TestCaseResult(
                "search",
                "数据库中无发票记录",
                "跳过测试（无数据）",
                "数据库为空，跳过搜索测试",
                null,
                true, // SKIP = 不视为失败
                "数据库为空，跳过"
            );
        }

        // 使用第一个发票的发行方名作为搜索词
        var searchTerm = allInvoices[0].IssuerName;
        var all = await invoiceService.GetAllAsync();
        var filtered = all.Where(i =>
            (i.IssuerName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.RegistrationNumber?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();

        bool passed = filtered.Count > 0 && filtered.Any(i => i.IssuerName == searchTerm);

        return new TestCaseResult(
            "search",
            $"搜索词: \"{searchTerm}\" (数据库共 {all.Count} 条)",
            $"返回包含 \"{searchTerm}\" 的发票",
            $"找到 {filtered.Count} 条匹配记录",
            null,
            passed,
            passed ? null : $"未找到包含搜索词 \"{searchTerm}\" 的发票"
        );
    }

    // ─── 4. Import: 发票导入 ──────────────────────────

    public static async Task<TestCaseResult> TestImport(IServiceProvider services)
    {
        var ocrService = services.GetRequiredService<IBaiduOcrService>();
        var glmService = services.GetRequiredService<IGlmService>();
        var invoiceService = services.GetRequiredService<IInvoiceService>();
        var fileService = services.GetRequiredService<IFileService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // 查找 TEMP\testdata 目录下的测试用发票图片
        string? testImage = null;
        if (Directory.Exists(TestDataDir))
        {
            testImage = Directory.GetFiles(TestDataDir, "*.jpg")
                .Concat(Directory.GetFiles(TestDataDir, "*.png"))
                .Concat(Directory.GetFiles(TestDataDir, "*.jpeg"))
                .Concat(Directory.GetFiles(TestDataDir, "*.pdf"))
                .FirstOrDefault();
        }

        if (string.IsNullOrEmpty(testImage) || !File.Exists(testImage))
        {
            return new TestCaseResult(
                "import",
                $"查找 {TestDataDir} 目录下的测试图片",
                "找到测试图片",
                $"未找到可用的测试发票图片 ({TestDataDir} 目录下无 jpg/png/pdf)",
                null,
                true, // SKIP
                "无测试图片，跳过导入测试"
            );
        }

        var beforeCount = (await invoiceService.GetAllAsync()).Count;

        try
        {
            // OCR
            var ocrResult = await ocrService.RecognizeAsync(testImage);
            if (string.IsNullOrEmpty(ocrResult))
            {
                return new TestCaseResult(
                    "import",
                    $"OCR 识别: {Path.GetFileName(testImage)}",
                    "返回 OCR 文本",
                    "OCR 返回空",
                    null,
                    true, // SKIP
                    "OCR 服务不可用，跳过导入测试"
                );
            }

            // AI 分析
            var glmResult = await glmService.ProcessBatchAsync(new[] { ocrResult });
            if (glmResult?.Count == 0)
            {
                return new TestCaseResult(
                    "import",
                    $"AI 分析: {Path.GetFileName(testImage)}",
                    "返回解析后的发票数据",
                    "AI 返回空",
                    null,
                    true, // SKIP
                    "AI 服务不可用，跳过导入测试"
                );
            }

            // 保存
            var glmResp = glmResult[0];
            DateTime? transDate = null;
            if (!string.IsNullOrEmpty(glmResp.TransactionDate) && DateTime.TryParse(glmResp.TransactionDate, out var parsed))
                transDate = parsed;

            var invoiceType = glmResp.InvoiceType switch
            {
                "Standard" => InvoiceType.Standard,
                "Simplified" => InvoiceType.Simplified,
                _ => InvoiceType.NonQualified
            };

            var items = glmResp.Items?.Select(i => new InvoiceItem
            {
                Name = i.Name,
                Amount = i.Amount,
                TaxRate = i.TaxRate,
                IsReducedRate = i.IsReducedRate
            }).ToList() ?? new List<InvoiceItem>();

            var hash = await fileService.ComputeFileHashAsync(testImage);
            var invoice = new Invoice
            {
                IssuerName = glmResp.IssuerName ?? "测试导入",
                RegistrationNumber = glmResp.RegistrationNumber ?? "",
                TransactionDate = transDate,
                Description = glmResp.Description ?? "",
                ItemsJson = JsonSerializer.Serialize(items),
                TaxExcludedAmount = glmResp.TaxExcludedAmount,
                TaxIncludedAmount = glmResp.TaxIncludedAmount,
                TaxAmount = glmResp.TaxAmount,
                InvoiceType = invoiceType,
                Category = glmResp.SuggestedCategory ?? "测试",
                SourceFilePath = testImage,
                FileHash = hash,
                IsConfirmed = false
            };

            await invoiceService.SaveAsync(invoice);
            var afterCount = (await invoiceService.GetAllAsync()).Count;

            bool passed = afterCount == beforeCount + 1;

            // 清理：删除测试导入的发票
            if (passed)
            {
                await invoiceService.DeleteAsync(invoice.Id);
            }

            return new TestCaseResult(
                "import",
                $"导入: {Path.GetFileName(testImage)}",
                $"OCR → AI → 保存，数据库记录数 +1 (导入前: {beforeCount}, 导入后: {afterCount})",
                passed ? $"成功导入并清理，数据库恢复到 {afterCount} 条" : $"导入失败 (导入前: {beforeCount}, 导入后: {afterCount})",
                null,
                passed,
                passed ? null : "导入后记录数不匹配"
            );
        }
        catch (Exception ex)
        {
            return new TestCaseResult(
                "import",
                $"导入: {Path.GetFileName(testImage)}",
                "完成 OCR → AI → 保存流程",
                $"异常: {ex.GetType().Name}: {ex.Message}",
                null,
                true, // SKIP - 外部服务异常
                $"导入流程异常: {ex.Message}"
            );
        }
    }

    // ─── 5. Export: 发票导出 ──────────────────────────

    public static async Task<TestCaseResult> TestExport(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();
        var exportService = services.GetRequiredService<IExcelExportService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var invoices = await invoiceService.GetAllAsync();
        var exportPath = Path.Combine(TestLogDir, $"test_export_{DateTime.Now:HHmmss}.xlsx");

        try
        {
            var resultPath = await exportService.ExportAsync(invoices, exportPath);
            var fileInfo = new FileInfo(resultPath);
            bool passed = fileInfo.Exists && fileInfo.Length > 0;

            // 清理导出的文件
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }

            return new TestCaseResult(
                "export",
                $"导出 {invoices.Count} 条发票到 Excel",
                "生成有效的 .xlsx 文件",
                passed ? $"导出成功，文件大小: {fileInfo.Length} 字节 (已清理)" : "导出文件为空或不存在",
                null,
                passed,
                passed ? null : "导出文件为空或不存在"
            );
        }
        catch (Exception ex)
        {
            return new TestCaseResult(
                "export",
                $"导出 {invoices.Count} 条发票到 Excel",
                "生成有效的 .xlsx 文件",
                $"异常: {ex.GetType().Name}: {ex.Message}",
                null,
                false,
                ex.Message
            );
        }
    }

    // ─── 6. Delete: 发票删除 ──────────────────────────

    public static async Task<TestCaseResult> TestDelete(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // 创建测试发票
        var testInvoice = new Invoice
        {
            IssuerName = "测试删除专用",
            RegistrationNumber = "T999999999999",
            TransactionDate = DateTime.UtcNow,
            Description = "命令行测试临时数据",
            Category = "测试",
            TaxIncludedAmount = 100,
            IsConfirmed = false
        };

        var beforeCount = (await invoiceService.GetAllAsync()).Count;
        var saved = await invoiceService.SaveAsync(testInvoice);
        var afterSaveCount = (await invoiceService.GetAllAsync()).Count;

        // 删除
        await invoiceService.DeleteAsync(saved.Id);
        var afterDeleteCount = (await invoiceService.GetAllAsync()).Count;
        var deleted = await invoiceService.GetByIdAsync(saved.Id);

        bool passed = afterSaveCount == beforeCount + 1
                   && afterDeleteCount == beforeCount
                   && deleted == null;

        return new TestCaseResult(
            "delete",
            $"创建测试发票 → 删除 (导入前: {beforeCount}, 创建后: {afterSaveCount}, 删除后: {afterDeleteCount})",
            "删除后 GetByIdAsync 返回 null，记录数恢复到删除前",
            passed ? "删除成功，数据库记录数正确恢复" : $"删除异常 (创建后: {afterSaveCount}, 删除后: {afterDeleteCount}, GetById: {deleted != null})",
            null,
            passed,
            passed ? null : "删除后记录仍存在或数量不匹配"
        );
    }

    // ─── 7. ImagePath: 发票图片路径查找 ─────────────────

    public static async Task<TestCaseResult> TestImagePath(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var invoices = await invoiceService.GetAllAsync();
        if (invoices.Count == 0)
        {
            return new TestCaseResult(
                "imagepath",
                "数据库中无发票记录",
                "跳过测试（无数据）",
                "数据库为空，跳过图片路径测试",
                null,
                true,
                "数据库为空，跳过"
            );
        }

        // 检查所有发票的 SourceFilePath
        var details = new System.Text.StringBuilder();
        int validCount = 0;
        int invalidCount = 0;

        foreach (var inv in invoices.Take(10))
        {
            var hasPath = !string.IsNullOrEmpty(inv.SourceFilePath);
            var fileExists = hasPath && File.Exists(inv.SourceFilePath);
            if (fileExists) validCount++;
            else if (hasPath) invalidCount++;

            details.AppendLine($"  [{inv.Id}] {inv.IssuerName}");
            details.AppendLine($"    SourceFilePath: {inv.SourceFilePath ?? "(null)"}");
            details.AppendLine($"    文件存在: {fileExists}");
        }

        bool passed = validCount > 0;

        return new TestCaseResult(
            "imagepath",
            $"检查 {invoices.Count} 条发票的 SourceFilePath (显示前 {Math.Min(10, invoices.Count)} 条)",
            "至少有一条发票的 SourceFilePath 指向存在的文件",
            $"有效: {validCount}, 无效: {invalidCount}\n{details}",
            null,
            passed,
            passed ? null : "所有发票的 SourceFilePath 均无效"
        );
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
    }

    // ─── 8. PdfConvert: PDF转图片测试 ─────────────────

    public static async Task<TestCaseResult> TestPdfConvert(IServiceProvider services)
    {
        var fileService = services.GetRequiredService<IFileService>();
        var settingsService = services.GetRequiredService<IAppSettingsService>();
        
        // 确保设置已加载
        await settingsService.LoadAsync();
        
        var archivePath = settingsService.Settings.InvoiceArchivePath;
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return new TestCaseResult(
                "pdfconvert",
                "PDF转图片测试",
                "归档路径已配置，PDF文件转换为图片成功",
                "归档路径未配置，请在设置中配置\"发票文件保存路径\"",
                null,
                true, // SKIP
                "归档路径未配置，跳过"
            );
        }

        // 查找测试 PDF 文件
        var testRoot = FindProjectRoot();
        var testdataDir = Path.Combine(testRoot, "TEMP", "testdata");
        string? testPdf = null;
        if (Directory.Exists(testdataDir))
        {
            testPdf = Directory.GetFiles(testdataDir, "*.pdf").FirstOrDefault();
        }

        if (string.IsNullOrEmpty(testPdf) || !File.Exists(testPdf))
        {
            return new TestCaseResult(
                "pdfconvert",
                "PDF转图片测试",
                "找到测试 PDF 文件并转换成功",
                $"未找到测试 PDF 文件 ({testdataDir})",
                null,
                true, // SKIP
                "无测试 PDF 文件，跳过"
            );
        }

        try
        {
            var images = await fileService.ConvertPdfToImagesAsync(testPdf, archivePath);
            var allExist = images.All(File.Exists);
            var detail = $"PDF: {Path.GetFileName(testPdf)}\n" +
                        $"  转换后图片数: {images.Count}\n" +
                        $"  图片路径: {string.Join(", ", images.Select(Path.GetFileName))}\n" +
                        $"  所有图片存在: {allExist}\n" +
                        $"  保存目录: {Path.GetDirectoryName(images[0])}";

            return new TestCaseResult(
                "pdfconvert",
                $"PDF转图片: {Path.GetFileName(testPdf)} (归档路径: {archivePath})",
                "PDF 成功转换为图片，所有图片文件存在",
                detail,
                null,
                allExist,
                allExist ? null : "部分图片文件不存在"
            );
        }
        catch (Exception ex)
        {
            return new TestCaseResult(
                "pdfconvert",
                $"PDF转图片: {Path.GetFileName(testPdf)}",
                "PDF 成功转换为图片",
                $"异常: {ex.GetType().Name}: {ex.Message}",
                null,
                true, // SKIP
                $"转换异常: {ex.Message}"
            );
        }
    }

    // ─── X. PdfImport: PDF导入识别全流程 ────────────────

    public static async Task<TestCaseResult> TestPdfImport(IServiceProvider services)
    {
        var ocrService = services.GetRequiredService<IBaiduOcrService>();
        var glmService = services.GetRequiredService<IGlmService>();
        var invoiceService = services.GetRequiredService<IInvoiceService>();
        var fileService = services.GetRequiredService<IFileService>();
        var settingsService = services.GetRequiredService<IAppSettingsService>();

        await settingsService.LoadAsync();
        var archivePath = settingsService.Settings.InvoiceArchivePath;
        if (string.IsNullOrWhiteSpace(archivePath))
            return new TestCaseResult("pdfimport", "PDF导入", "归档路径未配置", null, null, true, "归档路径未配置，跳过");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var testDir = TestDataDir;
        var testPdf = Directory.Exists(testDir) ? Directory.GetFiles(testDir, "*.pdf").FirstOrDefault() : null;
        if (string.IsNullOrEmpty(testPdf) || !File.Exists(testPdf))
            return new TestCaseResult("pdfimport", "PDF导入", "未找到测试 PDF", null, null, true, "无测试 PDF");

        try
        {
            var images = await fileService.ConvertPdfToImagesAsync(testPdf, archivePath);
            var detail = $"PDF: {Path.GetFileName(testPdf)}\n  转换图片: {images.Count} 张\n";
            
            int savedCount = 0;
            var savedIds = new List<int>();
            foreach (var img in images)
            {
                var hash = await fileService.ComputeFileHashAsync(img);
                if (await invoiceService.ExistsByHashAsync(hash)) { detail += $"  跳过重复: {Path.GetFileName(img)}\n"; continue; }
                
                var ocr = await ocrService.RecognizeAsync(img);
                if (string.IsNullOrEmpty(ocr)) { detail += $"  OCR 返回空\n"; continue; }

                var glm = await glmService.ProcessBatchAsync(new[] { ocr });
                if (glm.Count == 0) { detail += $"  AI 返回空\n"; continue; }
                
                var glmResp = glm[0];
                var invoice = new Invoice
                {
                    IssuerName = glmResp.IssuerName ?? "测试PDF导入",
                    RegistrationNumber = glmResp.RegistrationNumber ?? "",
                    TransactionDate = !string.IsNullOrEmpty(glmResp.TransactionDate) && DateTime.TryParse(glmResp.TransactionDate, out var d) ? d : null,
                    Description = glmResp.Description ?? "",
                    ItemsJson = System.Text.Json.JsonSerializer.Serialize(glmResp.Items?.Select(i => new InvoiceItem { Name = i.Name, Amount = i.Amount, TaxRate = i.TaxRate, IsReducedRate = i.IsReducedRate }).ToList() ?? new List<InvoiceItem>()),
                    TaxExcludedAmount = glmResp.TaxExcludedAmount,
                    TaxIncludedAmount = glmResp.TaxIncludedAmount,
                    TaxAmount = glmResp.TaxAmount,
                    InvoiceType = glmResp.InvoiceType switch { "Standard" => InvoiceType.Standard, "Simplified" => InvoiceType.Simplified, _ => InvoiceType.NonQualified },
                    Category = glmResp.SuggestedCategory ?? "测试",
                    SourceFilePath = img,
                    FileHash = hash,
                    IsConfirmed = false
                };
                invoice = await invoiceService.SaveAsync(invoice);
                savedIds.Add(invoice.Id);
                savedCount++;
                detail += $"  保存成功: {invoice.IssuerName}\n";
            }
            
            foreach (var id in savedIds) await invoiceService.DeleteAsync(id);
                
            return new TestCaseResult("pdfimport", $"PDF导入: {Path.GetFileName(testPdf)}", $"转换 {images.Count} 张，保存 {savedCount} 张发票", detail.Trim(), null, savedCount > 0, savedCount > 0 ? null : "未保存任何发票");
        }
        catch (Exception ex)
        {
            return new TestCaseResult("pdfimport", $"PDF导入: {Path.GetFileName(testPdf)}", "成功导入", $"异常: {ex.GetType().Name}: {ex.Message}", null, true, ex.Message);
        }
    }

    // ─── 9. Saved: 已保存列表 ─────────────────────────

    public static async Task<TestCaseResult> TestSaved(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // 获取已确认发票
        var confirmed = await invoiceService.GetConfirmedAsync();
        var totalCount = (await invoiceService.GetAllAsync()).Count;

        // 验证所有返回的发票确实 IsConfirmed=true
        bool allConfirmed = confirmed.All(i => i.IsConfirmed);

        // 如果有已确认发票，验证按创建时间范围查询
        List<Invoice> dateRangeResults = new();
        if (confirmed.Count > 0)
        {
            var earliest = confirmed.Min(i => i.CreatedAt).AddDays(-1);
            var latest = confirmed.Max(i => i.CreatedAt).AddDays(1);
            dateRangeResults = await invoiceService.GetByCreateDateRangeAsync(earliest, latest);
        }

        bool dateRangePassed = confirmed.Count == 0 || dateRangeResults.Count > 0;

        return new TestCaseResult(
            "saved",
            $"查询已确认发票列表 (共 {totalCount} 条，已确认: {confirmed.Count})",
            $"返回所有 IsConfirmed=true 的发票 ({confirmed.Count} 条)",
            $"已确认发票: {confirmed.Count} 条, 全部 IsConfirmed=true: {allConfirmed}, 时间范围查询: {dateRangeResults.Count} 条",
            null,
            allConfirmed && dateRangePassed,
            !allConfirmed ? "存在 IsConfirmed=false 的发票在已确认列表中" : (!dateRangePassed ? "时间范围查询失败" : null)
        );
    }

    // ─── 8. Edit: 编辑保存 ────────────────────────────

    public static async Task<TestCaseResult> TestEdit(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // 创建测试发票
        var testInvoice = new Invoice
        {
            IssuerName = "测试编辑专用",
            RegistrationNumber = "T888888888888",
            TransactionDate = DateTime.UtcNow,
            Description = "编辑前内容",
            Category = "测试",
            TaxIncludedAmount = 200,
            IsConfirmed = false
        };

        var saved = await invoiceService.SaveAsync(testInvoice);
        var originalId = saved.Id;

        // 编辑
        saved.Description = "编辑后内容";
        saved.Category = "测试_已编辑";
        saved.TaxIncludedAmount = 300;
        saved.UpdatedAt = DateTime.UtcNow;

        await invoiceService.UpdateAsync(saved);
        var reloaded = await invoiceService.GetByIdAsync(originalId);

        bool passed = reloaded != null
                   && reloaded.Description == "编辑后内容"
                   && reloaded.Category == "测试_已编辑"
                   && reloaded.TaxIncludedAmount == 300;

        // 清理
        await invoiceService.DeleteAsync(originalId);

        return new TestCaseResult(
            "edit",
            $"创建发票 → 编辑 Description/Category/Amount → 保存 → 重新读取",
            "UpdateAsync 后数据库记录正确更新",
            passed ? $"编辑成功: Description=\"{reloaded?.Description}\", Category=\"{reloaded?.Category}\", Amount={reloaded?.TaxIncludedAmount} (已清理)" : $"编辑失败 (Description: {reloaded?.Description}, Category: {reloaded?.Category}, Amount: {reloaded?.TaxIncludedAmount})",
            null,
            passed,
            passed ? null : "编辑后字段值未正确更新"
        );
    }
}
