# Provider Fallback During Import — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build automatic provider fallback mechanism that switches to verified backup providers when GLM API calls fail during invoice import.

**Architecture:** Introduce a `ProviderFallbackManager` to track user-verified providers, modify `InvoiceImportService` to catch GLM failures and switch providers temporarily, restore original provider after import completes.

**Tech Stack:** .NET 9, C# Markup MAUI, CommunityToolkit.Mvvm, EF Core SQLite, existing GLM multi-provider infrastructure (Zhipu, NVIDIA NIM, Cerebras).

---

## File Structure

```
src/InvoiceAI.Core/Helpers/
  LogHelper.cs (MODIFY - add Log method)
  AppSettings.cs (MODIFY - add VerifiedProviders property)

src/InvoiceAI.Core/Services/
  IProviderFallbackManager.cs (CREATE - new interface)
  ProviderFallbackManager.cs (CREATE - new service)
  IInvoiceImportService.cs (MODIFY - add StatusChanged event)
  InvoiceImportService.cs (MODIFY - add fallback logic in ProcessBatchAsync)
  IAppSettingsService.cs (NO CHANGE - already exists)

src/InvoiceAI.Core/ViewModels/
  SettingsViewModel.cs (MODIFY - call MarkProviderVerifiedAsync after successful test)
  ImportViewModel.cs (MODIFY - subscribe to StatusChanged event)

src/InvoiceAI.App/
  MauiProgram.cs (MODIFY - register IProviderFallbackManager)

tests/InvoiceAI.Core.Tests/
  ProviderFallbackManagerTests.cs (CREATE - unit tests)
  InvoiceImportServiceFallbackTests.cs (CREATE - integration tests)
```

---

## Task 1: Enhance LogHelper with Log Method

**Files:**
- Modify: `src/InvoiceAI.Core/Helpers/LogHelper.cs`
- Test: (None - helper method)

- [ ] **Step 1: Read existing LogHelper implementation**

Read: `src/InvoiceAI.Core/Helpers/LogHelper.cs`
Expected: See only `LogDir` property, no `Log` method

- [ ] **Step 2: Add Log method to LogHelper**

```csharp
public static void Log(string message)
{
    try
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logMessage = $"[{timestamp}] {message}";
        var logFile = Path.Combine(LogDir, "provider_fallback.log");
        File.AppendAllText(logFile, logMessage + Environment.NewLine);
    }
    catch
    {
        // Silently fail - logging should not break the app
    }
}
```

- [ ] **Step 3: Build to verify no errors**

Run: `dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj`
Expected: BUILD SUCCESS

- [ ] **Step 4: Commit**

```bash
git add src/InvoiceAI.Core/Helpers/LogHelper.cs
git commit -m "feat: add Log method to LogHelper for provider fallback logging"
```

---

## Task 2: Add VerifiedProviders to GlmSettings

**Files:**
- Modify: `src/InvoiceAI.Core/Helpers/AppSettings.cs`
- Test: `tests/InvoiceAI.Core.Tests/SettingsViewModelTests.cs` (verify backward compatibility)

- [ ] **Step 1: Write test for VerifiedProviders backward compatibility**

```csharp
[Fact]
public void GlmSettings_VerifiedProviders_DefaultsToEmptyList()
{
    // Arrange & Act
    var settings = new AppSettings();

    // Assert
    Assert.NotNull(settings.Glm.VerifiedProviders);
    Assert.Empty(settings.Glm.VerifiedProviders);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~GlmSettings_VerifiedProviders_DefaultsToEmptyList"`
Expected: FAIL with "VerifiedProviders does not exist"

- [ ] **Step 3: Add VerifiedProviders property to GlmSettings**

In `src/InvoiceAI.Core/Helpers/AppSettings.cs`, add to `GlmSettings` class:

```csharp
public class GlmSettings
{
    public string Provider { get; set; } = "zhipu";

    // ... existing properties ...

    // NEW: List of providers verified by user through Settings page
    public List<string> VerifiedProviders { get; set; } = new();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~GlmSettings_VerifiedProviders_DefaultsToEmptyList"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/InvoiceAI.Core/Helpers/AppSettings.cs tests/InvoiceAI.Core.Tests/SettingsViewModelTests.cs
git commit -m "feat: add VerifiedProviders property to GlmSettings"
```

---

## Task 3: Create IProviderFallbackManager Interface

**Files:**
- Create: `src/InvoiceAI.Core/Services/IProviderFallbackManager.cs`
- Test: (None - interface only)

- [ ] **Step 1: Create the interface file**

Create: `src/InvoiceAI.Core/Services/IProviderFallbackManager.cs`

```csharp
namespace InvoiceAI.Core.Services;

/// <summary>
/// Manages provider fallback logic during import operations.
/// </summary>
public interface IProviderFallbackManager
{
    /// <summary>
    /// Marks a provider as verified (user tested connection successfully).
    /// </summary>
    /// <param name="provider">The provider ID (e.g., "zhipu", "nvidia", "cerebras")</param>
    Task MarkProviderVerifiedAsync(string provider);

    /// <summary>
    /// Attempts to get the next available verified provider after a failure.
    /// </summary>
    /// <param name="currentProvider">The provider that just failed</param>
    /// <param name="reason">Failure reason for logging</param>
    /// <returns>Next verified provider ID, or null if none available</returns>
    string? TryGetNextProvider(string currentProvider, string reason);

    /// <summary>
    /// Gets the list of all verified providers.
    /// </summary>
    IReadOnlyList<string> GetVerifiedProviders();

    /// <summary>
    /// Resets the current provider state (called after import completes).
    /// </summary>
    void Reset();
}
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj`
Expected: BUILD SUCCESS

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.Core/Services/IProviderFallbackManager.cs
git commit -m "feat: add IProviderFallbackManager interface"
```

---

## Task 4: Create ProviderFallbackManager Implementation

**Files:**
- Create: `src/InvoiceAI.Core/Services/ProviderFallbackManager.cs`
- Test: `tests/InvoiceAI.Core.Tests/ProviderFallbackManagerTests.cs`

- [ ] **Step 1: Write test for MarkProviderVerifiedAsync deduplication**

```csharp
public class ProviderFallbackManagerTests
{
    private readonly Mock<IAppSettingsService> _mockSettings;
    private readonly AppSettings _testSettings;
    private readonly ProviderFallbackManager _manager;

    public ProviderFallbackManagerTests()
    {
        _mockSettings = new Mock<IAppSettingsService>();
        _testSettings = new AppSettings();
        _mockSettings.Setup(s => s.Settings).Returns(_testSettings);
        _mockSettings.Setup(s => s.SaveAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _manager = new ProviderFallbackManager(_mockSettings.Object);
    }

    [Fact]
    public async Task MarkProviderVerifiedAsync_AddsNewProvider()
    {
        // Act
        await _manager.MarkProviderVerifiedAsync("zhipu");
        await _mockSettings.Object.SaveAsync();

        // Assert
        Assert.Contains("zhipu", _testSettings.Glm.VerifiedProviders);
        _mockSettings.Verify(s => s.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkProviderVerifiedAsync_DoesNotDuplicate()
    {
        // Arrange
        await _manager.MarkProviderVerifiedAsync("zhipu");
        await _mockSettings.Object.SaveAsync();
        _mockSettings.Invocations.Clear(); // Clear previous calls

        // Act
        await _manager.MarkProviderVerifiedAsync("zhipu");
        await _mockSettings.Object.SaveAsync();

        // Assert
        Assert.Single(_testSettings.Glm.VerifiedProviders);
        _mockSettings.Verify(s => s.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~MarkProviderVerifiedAsync"`
Expected: FAIL with "ProviderFallbackManager does not exist"

- [ ] **Step 3: Create ProviderFallbackManager implementation**

Create: `src/InvoiceAI.Core/Services/ProviderFallbackManager.cs`

```csharp
using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

/// <summary>
/// Manages provider fallback logic during import operations.
/// Tracks user-verified providers and provides sequential fallback.
/// </summary>
public class ProviderFallbackManager : IProviderFallbackManager
{
    private readonly IAppSettingsService _settingsService;

    public ProviderFallbackManager(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task MarkProviderVerifiedAsync(string provider)
    {
        var verified = _settingsService.Settings.Glm.VerifiedProviders;
        if (!verified.Contains(provider))
        {
            verified.Add(provider);
            await _settingsService.SaveAsync();
        }
    }

    /// <inheritdoc/>
    public string? TryGetNextProvider(string currentProvider, string reason)
    {
        var verified = _settingsService.Settings.Glm.VerifiedProviders;

        // Log the fallback attempt
        LogHelper.Log($"Provider fallback: {currentProvider} failed ({reason})");
        LogHelper.Log($"Verified providers: [{string.Join(", ", verified)}]");

        var currentIndex = verified.IndexOf(currentProvider);

        // Find next provider after current in the verified list
        for (int i = currentIndex + 1; i < verified.Count; i++)
        {
            var next = verified[i];
            LogHelper.Log($"Switching to provider: {next}");
            return next;
        }

        LogHelper.Log($"No more verified providers available");
        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetVerifiedProviders()
    {
        return _settingsService.Settings.Glm.VerifiedProviders.AsReadOnly();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // Provider state is managed by AppSettings.Glm.Provider
        // This method is a no-op but kept for interface symmetry
        LogHelper.Log("ProviderFallbackManager reset");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~MarkProviderVerifiedAsync"`
Expected: PASS

- [ ] **Step 5: Write test for TryGetNextProvider sequential logic**

```csharp
[Fact]
public void TryGetNextProvider_ReturnsNextInSequence()
{
    // Arrange
    _testSettings.Glm.VerifiedProviders.AddRange(new[] { "zhipu", "nvidia", "cerebras" });

    // Act
    var next = _manager.TryGetNextProvider("zhipu", "test failure");

    // Assert
    Assert.Equal("nvidia", next);
}

[Fact]
public void TryGetNextProvider_ReturnsNullWhenNoMoreProviders()
{
    // Arrange
    _testSettings.Glm.VerifiedProviders.AddRange(new[] { "zhipu" });

    // Act
    var next = _manager.TryGetNextProvider("zhipu", "test failure");

    // Assert
    Assert.Null(next);
}

[Fact]
public void GetVerifiedProviders_ReturnsReadOnlyList()
{
    // Arrange
    _testSettings.Glm.VerifiedProviders.AddRange(new[] { "zhipu", "nvidia" });

    // Act
    var verified = _manager.GetVerifiedProviders();

    // Assert
    Assert.Equal(2, verified.Count);
    Assert.IsAssignableFrom<IReadOnlyList<string>>(verified);
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~TryGetNextProvider"`
Expected: PASS

- [ ] **Step 7: Build full solution to verify no errors**

Run: `dotnet build InvoiceAI.sln`
Expected: BUILD SUCCESS

- [ ] **Step 8: Commit**

```bash
git add src/InvoiceAI.Core/Services/ProviderFallbackManager.cs tests/InvoiceAI.Core.Tests/ProviderFallbackManagerTests.cs
git commit -m "feat: implement ProviderFallbackManager with verified provider tracking"
```

---

## Task 5: Add StatusChanged Event to IInvoiceImportService

**Files:**
- Modify: `src/InvoiceAI.Core/Services/IInvoiceImportService.cs`
- Test: (None - interface change, implementation will follow)

- [ ] **Step 1: Add event to interface**

Modify `src/InvoiceAI.Core/Services/IInvoiceImportService.cs`:

```csharp
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
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj`
Expected: BUILD SUCCESS (but InvoiceImportService implementation will now show missing event)

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.Core/Services/IInvoiceImportService.cs
git commit -m "feat: add StatusChanged event to IInvoiceImportService"
```

---

## Task 6: Implement Fallback Logic in InvoiceImportService

**Files:**
- Modify: `src/InvoiceAI.Core/Services/InvoiceImportService.cs`
- Test: `tests/InvoiceAI.Core.Tests/InvoiceImportServiceFallbackTests.cs`

- [ ] **Step 1: Write integration test for provider fallback**

```csharp
public class InvoiceImportServiceFallbackTests : IDisposable
{
    private readonly Mock<IBaiduOcrService> _mockOcr;
    private readonly Mock<IGlmService> _mockGlm;
    private readonly Mock<IInvoiceService> _mockInvoice;
    private readonly Mock<IFileService> _mockFile;
    private readonly Mock<IAppSettingsService> _mockSettings;
    private readonly Mock<IProviderFallbackManager> _mockFallback;
    private readonly InvoiceImportService _service;
    private readonly AppSettings _testSettings;
    private readonly string _testFile;

    public InvoiceImportServiceFallbackTests()
    {
        _mockOcr = new Mock<IBaiduOcrService>();
        _mockGlm = new Mock<IGlmService>();
        _mockInvoice = new Mock<IInvoiceService>();
        _mockFile = new Mock<IFileService>();
        _mockSettings = new Mock<IAppSettingsService>();
        _mockFallback = new Mock<IProviderFallbackManager>();

        _testSettings = new AppSettings();
        _mockSettings.Setup(s => s.Settings).Returns(_testSettings);

        _testFile = Path.GetTempFileName() + ".jpg";
        File.WriteAllText(_testFile, "test");

        _mockFile.Setup(f => f.FilterSupportedFiles(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<string> { _testFile });
        _mockFile.Setup(f => f.ComputeFileHashAsync(It.IsAny<string>()))
            .ReturnsAsync("test-hash");
        _mockInvoice.Setup(i => i.ExistsByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockInvoice.Setup(i => i.SaveAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice inv) => inv);
        _mockOcr.Setup(o => o.RecognizeAsync(It.IsAny<string>()))
            .ReturnsAsync("OCR text");

        _service = new InvoiceImportService(
            _mockOcr.Object,
            _mockGlm.Object,
            _mockInvoice.Object,
            _mockFile.Object,
            _mockSettings.Object,
            _mockFallback.Object);
    }

    public void Dispose()
    {
        if (File.Exists(_testFile)) File.Delete(_testFile);
    }

    [Fact]
    public async Task ImportAsync_WhenGlmFails_TriesNextProvider()
    {
        // Arrange
        _testSettings.Glm.Provider = "zhipu";
        _testSettings.Glm.VerifiedProviders.AddRange(new[] { "zhipu", "nvidia" });

        _mockGlm.SetupSequence(g => g.ProcessBatchAsync(It.IsAny<string[]>()))
            .ThrowsAsync(new HttpRequestException("Network error"))
            .ReturnsAsync(new List<GlmInvoiceResponse> { new() });

        _mockFallback.Setup(f => f.TryGetNextProvider("zhipu", It.IsAny<string>()))
            .Returns("nvidia");

        var statusMessages = new List<string>();
        _service.StatusChanged += (s, msg) => statusMessages.Add(msg);

        // Act
        var result = await _service.ImportAsync(new[] { _testFile });

        // Assert
        _mockFallback.Verify(f => f.TryGetNextProvider("zhipu", It.IsAny<string>()), Times.Once);
        Assert.Equal("nvidia", _testSettings.Glm.Provider); // Should have switched
    }

    [Fact]
    public async Task ImportAsync_RestoresOriginalProvider_AfterImport()
    {
        // Arrange
        _testSettings.Glm.Provider = "zhipu";
        _testSettings.Glm.VerifiedProviders.Add("zhipu");

        _mockGlm.Setup(g => g.ProcessBatchAsync(It.IsAny<string[]>()))
            .ReturnsAsync(new List<GlmInvoiceResponse> { new() });

        // Act
        await _service.ImportAsync(new[] { _testFile });

        // Assert
        Assert.Equal("zhipu", _testSettings.Glm.Provider);
        _mockFallback.Verify(f => f.Reset(), Times.Once);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~InvoiceImportServiceFallbackTests"`
Expected: FAIL - constructor doesn't accept IProviderFallbackManager, no StatusChanged event implementation

- [ ] **Step 3: Modify InvoiceImportService constructor and add event**

Modify `src/InvoiceAI.Core/Services/InvoiceImportService.cs`:

```csharp
public class InvoiceImportService : IInvoiceImportService
{
    private readonly IBaiduOcrService _ocrService;
    private readonly IGlmService _glmService;
    private readonly IInvoiceService _invoiceService;
    private readonly IFileService _fileService;
    private readonly IAppSettingsService _settingsService;
    private readonly IProviderFallbackManager _fallbackManager;

    private const int BatchSize = 5;

    public event EventHandler<string>? StatusChanged;

    public InvoiceImportService(
        IBaiduOcrService ocrService,
        IGlmService glmService,
        IInvoiceService invoiceService,
        IFileService fileService,
        IAppSettingsService settingsService,
        IProviderFallbackManager fallbackManager)
    {
        _ocrService = ocrService;
        _glmService = glmService;
        _invoiceService = invoiceService;
        _fileService = fileService;
        _settingsService = settingsService;
        _fallbackManager = fallbackManager;
    }

    private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);

    // ... rest of existing code ...
```

- [ ] **Step 4: Modify ImportAsync to save/restore provider**

Modify the `ImportAsync` method in `InvoiceImportService.cs`:

```csharp
public async Task<ImportResult> ImportAsync(IEnumerable<string> filePaths, Action<int, int>? onProgress = null)
{
    var originalProvider = _settingsService.Settings.Glm.Provider;
    try
    {
        var supported = _fileService.FilterSupportedFiles(filePaths);
        var result = new ImportResult { TotalFiles = supported.Count };

        if (supported.Count == 0) return result;

        var allImageItems = new List<ImageItem>();
        var pdfFailedFiles = new List<string>();

        // ... existing Phase 0 code ...

        // Process in batches
        int totalBatches = (result.TotalImages + BatchSize - 1) / BatchSize;
        for (int batch = 0; batch < totalBatches; batch++)
        {
            onProgress?.Invoke(batch + 1, totalBatches);

            var batchStart = batch * BatchSize;
            var batchEnd = Math.Min(batchStart + BatchSize, result.TotalImages);
            var batchItems = allImageItems.Skip(batchStart).Take(batchEnd - batchStart).ToList();

            await ProcessBatchAsync(batchItems, result);
        }

        return result;
    }
    finally
    {
        // Restore original provider after import completes
        _settingsService.Settings.Glm.Provider = originalProvider;
        _fallbackManager.Reset();
    }
}
```

- [ ] **Step 5: Modify ProcessBatchAsync to add fallback logic**

Replace the Phase 3 section in `ProcessBatchAsync`:

```csharp
// Phase 3: GLM AI — with fallback logic
var ocrTexts = ocrSuccessIndices.Select(i => ocrResults[i].Text).ToArray();
List<GlmInvoiceResponse>? glmResults = null;

var currentProvider = _settingsService.Settings.Glm.Provider;
var switchedProvider = false;

try
{
    glmResults = await _glmService.ProcessBatchAsync(ocrTexts);

    // Validate response
    if (glmResults == null || glmResults.Count == 0)
        throw new InvalidOperationException("GLM 返回结果为空");

    var allFailed = glmResults.All(r => r.MissingFields?.Count > 5);
    if (allFailed)
        throw new InvalidOperationException("GLM 返回结果全部标记为缺失字段");
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    // Try fallback to next verified provider
    var nextProvider = _fallbackManager.TryGetNextProvider(currentProvider, ex.Message);
    if (nextProvider != null)
    {
        _settingsService.Settings.Glm.Provider = nextProvider;
        switchedProvider = true;
        OnStatusChanged($"当前提供商连接失败，已切换到 {GetProviderDisplayName(nextProvider)} 继续处理");

        try
        {
            glmResults = await _glmService.ProcessBatchAsync(ocrTexts);
            if (glmResults == null || glmResults.Count == 0)
                throw new InvalidOperationException("切换提供商后 GLM 返回结果仍为空");
        }
        catch (Exception retryEx)
        {
            HandleFallbackFailure(batchItems, result, retryEx);
            return;
        }
    }
    else
    {
        HandleFallbackFailure(batchItems, result, ex);
        return;
    }
}

// Phase 4: Save and Archive (only if we have valid results)
if (glmResults != null)
{
    // ... existing Phase 4 code ...
}
```

- [ ] **Step 6: Add helper methods at the end of the class**

```csharp
private void HandleFallbackFailure(List<ImageItem> batchItems, ImportResult result, Exception ex)
{
    foreach (var item in batchItems)
    {
        result.Errors.Add($"AI 分析失败（所有可用提供商均已失败）: {item.FilePath}");
    }
    OnStatusChanged("所有已验证提供商均失败，请检查网络连接或提供商设置");
}

private static string GetProviderDisplayName(string providerId) => providerId switch
{
    "zhipu" => "智谱 (Zhipu)",
    "nvidia" => "NVIDIA NIM",
    "cerebras" => "Cerebras",
    _ => providerId
};
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~InvoiceImportServiceFallbackTests"`
Expected: PASS

- [ ] **Step 8: Build full solution**

Run: `dotnet build InvoiceAI.sln`
Expected: BUILD SUCCESS

- [ ] **Step 9: Commit**

```bash
git add src/InvoiceAI.Core/Services/InvoiceImportService.cs tests/InvoiceAI.Core.Tests/InvoiceImportServiceFallbackTests.cs
git commit -m "feat: implement provider fallback logic in InvoiceImportService"
```

---

## Task 7: Integrate ProviderFallbackManager with SettingsViewModel

**Files:**
- Modify: `src/InvoiceAI.Core/ViewModels/SettingsViewModel.cs`
- Test: Update `tests/InvoiceAI.Core.Tests/SettingsViewModelTests.cs`

- [ ] **Step 1: Write test for MarkProviderVerifiedAsync call**

```csharp
[Fact]
public async Task TestGlmConnectionAsync_WhenSuccessful_MarksProviderVerified()
{
    // Arrange
    var mockFallback = new Mock<IProviderFallbackManager>();
    var vm = new SettingsViewModel(
        _mockSettings.Object,
        null, null, _mockHttpClient.Object,
        mockFallback.Object);

    vm.GlmApiKey = "test-key";
    vm.GlmEndpoint = "https://test.com";
    vm.GlmModel = "test-model";

    _mockHttpClient.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

    // Act
    await vm.TestGlmConnectionAsyncCommand.ExecuteAsync(null);

    // Assert
    mockFallback.Verify(f => f.MarkProviderVerifiedAsync(It.IsAny<string>()), Times.Once);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~TestGlmConnectionAsync_WhenSuccessful_MarksProviderVerified"`
Expected: FAIL - SettingsViewModel doesn't accept IProviderFallbackManager

- [ ] **Step 3: Modify SettingsViewModel constructor**

In `src/InvoiceAI.Core/ViewModels/SettingsViewModel.cs`:

```csharp
private readonly IAppSettingsService _settingsService;
private readonly IBaiduOcrService? _ocrService;
private readonly IGlmService? _glmService;
private readonly HttpClient? _httpClient;
private readonly IProviderFallbackManager? _fallbackManager;  // NEW

public SettingsViewModel(
    IAppSettingsService settingsService,
    IBaiduOcrService? ocrService = null,
    IGlmService? glmService = null,
    HttpClient? httpClient = null,
    IProviderFallbackManager? fallbackManager = null)  // NEW
{
    _settingsService = settingsService;
    _ocrService = ocrService;
    _glmService = glmService;
    _httpClient = httpClient;
    _fallbackManager = fallbackManager;
    // ... rest of constructor unchanged ...
}
```

- [ ] **Step 4: Modify TestGlmConnectionAsync to mark provider verified**

Find the success case in `TestGlmConnectionAsync` and add:

```csharp
if (response.StatusCode == System.Net.HttpStatusCode.OK)
{
    TestResult = $"{providerName} 连接成功";
    // NEW: Mark provider as verified when connection succeeds
    if (_fallbackManager != null)
    {
        await _fallbackManager.MarkProviderVerifiedAsync(GlmProvider);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~TestGlmConnectionAsync_WhenSuccessful_MarksProviderVerified"`
Expected: PASS

- [ ] **Step 6: Run all SettingsViewModelTests to verify no regression**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelTests"`
Expected: All existing tests PASS

- [ ] **Step 7: Commit**

```bash
git add src/InvoiceAI.Core/ViewModels/SettingsViewModel.cs tests/InvoiceAI.Core.Tests/SettingsViewModelTests.cs
git commit -m "feat: mark provider as verified after successful connection test in SettingsViewModel"
```

---

## Task 8: Subscribe to StatusChanged Event in ImportViewModel

**Files:**
- Modify: `src/InvoiceAI.Core/ViewModels/ImportViewModel.cs`
- Test: Update `tests/InvoiceAI.Core.Tests/ImportViewModelTests.cs`

- [ ] **Step 1: Write test for StatusChanged event subscription**

```csharp
[Fact]
public void Constructor_SubscribesToStatusChangedEvent()
{
    // Arrange
    var mockImport = new Mock<IInvoiceImportService>();
    var statusRaised = false;
    mockImport.Setup(i => i.ImportAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<Action<int, int>?>()))
        .ReturnsAsync(new ImportResult());
    mockImport.Setup(i => i.StatusChanged += It.IsAny<EventHandler<string>>())
        .Callback<EventHandler<string>>(h =>
        {
            h.Invoke(mockImport.Object, "test message");
            statusRaised = true;
        });

    // Act
    var vm = new ImportViewModel(mockImport.Object, _mockFile.Object);

    // Assert
    // StatusChanged subscription happens in constructor
    mockImport.VerifyAdd(i => i.StatusChanged += It.IsAny<EventHandler<string>>(), Times.Once);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~Constructor_SubscribesToStatusChangedEvent"`
Expected: FAIL - ImportViewModel doesn't subscribe to StatusChanged

- [ ] **Step 3: Modify ImportViewModel constructor**

In `src/InvoiceAI.Core/ViewModels/ImportViewModel.cs`:

```csharp
public ImportViewModel(
    IInvoiceImportService importService,
    IFileService fileService)
{
    _importService = importService;
    _fileService = fileService;

    // Subscribe to import status changes
    _importService.StatusChanged += OnImportStatusChanged;
}

private void OnImportStatusChanged(object? sender, string message)
{
    // Ensure UI update happens on main thread
    MainThread.BeginInvokeOnMainThread(() => StatusMessage = message);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~Constructor_SubscribesToStatusChangedEvent"`
Expected: PASS

- [ ] **Step 5: Run all ImportViewModelTests to verify no regression**

Run: `dotnet test tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj --filter "FullyQualifiedName~ImportViewModelTests"`
Expected: All existing tests PASS

- [ ] **Step 6: Commit**

```bash
git add src/InvoiceAI.Core/ViewModels/ImportViewModel.cs tests/InvoiceAI.Core.Tests/ImportViewModelTests.cs
git commit -m "feat: subscribe to StatusChanged event in ImportViewModel for provider fallback notifications"
```

---

## Task 9: Register IProviderFallbackManager in DI Container

**Files:**
- Modify: `src/InvoiceAI.App/MauiProgram.cs`
- Test: Manual verification (app builds and runs)

- [ ] **Step 1: Add provider registration to MauiProgram**

In `src/InvoiceAI.App/MauiProgram.cs`, add after other service registrations:

```csharp
// Singleton services
builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddSingleton<IBaiduOcrService, BaiduOcrService>();
builder.Services.AddSingleton<IGlmService, GlmService>();
builder.Services.AddSingleton<IInvoiceService, InvoiceService>();
builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();
builder.Services.AddSingleton<IProviderFallbackManager, ProviderFallbackManager>();  // NEW

// Import Service (Core Logic)
builder.Services.AddSingleton<IInvoiceImportService, InvoiceImportService>();
```

- [ ] **Step 2: Build full solution**

Run: `dotnet build InvoiceAI.sln`
Expected: BUILD SUCCESS

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.App/MauiProgram.cs
git commit -m "feat: register IProviderFallbackManager in DI container"
```

---

## Task 10: End-to-End Integration Testing

**Files:**
- Manual testing with running app
- Verification: Provider fallback works in real import scenario

- [ ] **Step 1: Build and run the Windows app**

Run: `dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -f net9.0-windows10.0.19041.0`
Expected: App launches successfully

- [ ] **Step 2: Test provider verification in Settings page**

1. Navigate to Settings page
2. Configure Zhipu provider credentials
3. Click "Test Connection"
4. Verify: Connection succeeds
5. Check: `VerifiedProviders` should now contain "zhipu" in appsettings.json

- [ ] **Step 3: Test provider fallback during import**

1. In Settings, add another provider (e.g., NVIDIA) and test connection
2. Both "zhipu" and "nvidia" should be in VerifiedProviders
3. Intentionally break Zhipu (e.g., wrong API key)
4. Import a test invoice
5. Verify: StatusMessage shows "已切换到 NVIDIA NIM 继续处理"
6. Verify: Import completes successfully using NVIDIA

- [ ] **Step 4: Test provider restoration after import**

1. After import completes, check Settings page
2. Verify: Selected provider is back to original (Zhipu)
3. appsettings.json should still show original provider

- [ ] **Step 5: Test no fallback when all providers fail**

1. Break all verified providers' credentials
2. Try to import
3. Verify: Error message "所有已验证提供商均失败"
4. Verify: No items imported

- [ ] **Step 6: Commit final integration notes**

```bash
git commit --allow-empty -m "test: verify provider fallback end-to-end integration"
```

---

## Post-Implementation Checklist

- [ ] **All tests pass**: Run `dotnet test InvoiceAI.sln`
- [ ] **Solution builds**: Run `dotnet build InvoiceAI.sln --configuration Release`
- [ ] **App launches**: Test on Windows desktop
- [ ] **Settings page works**: Test connection for multiple providers
- [ ] **Import with fallback**: Test with failing primary provider
- [ ] **Provider restoration**: Verify original provider restored after import
- [ ] **Log file created**: Check `TEMP/provider_fallback.log` exists after fallback
- [ ] **No regressions**: Verify existing import/export functionality still works

---

## Reference Skills

- @superpowers:test-driven-development - All implementation follows TDD cycle
- @superpowers:systematic-debugging - Use if tests fail unexpectedly
- @superpowers:verification-before-completion - Use before declaring feature complete
