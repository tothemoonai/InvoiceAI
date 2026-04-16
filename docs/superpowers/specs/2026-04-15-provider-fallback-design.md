# Provider Fallback During Import — Design Document

> 日期: 2026-04-15
> 状态: 待实现

## 需求概述

用户在执行发票导入时，如果当前大模型提供商返回异常（HTTP错误、解析失败、业务逻辑错误），系统自动切换到下一个**用户已测试通过**的提供商继续处理。设置页面配置保持不变（临时切换）。

## 架构设计

```
┌──────────────────────────────────────────────────────────────────┐
│                        SettingsPage/ViewModel                     │
│  测试连接成功 → 调用 ProviderFallbackManager.MarkProviderVerified │
│                            Async                                  │
└────────────────────────┬─────────────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────────────┐
│                    ProviderFallbackManager                        │
│  - verifiedProviders: List<string> (持久化到AppSettings)          │
│  - currentProvider: string (内存状态)                             │
│  - MarkProviderVerified(provider)                                 │
│  - TryGetNextProvider(current, reason) → nextProvider or null     │
│  - ResetToDefault()                                               │
└───────┬──────────────────────────────────┬───────────────────────┘
        │ 读取/写入                         │ 读取
        ▼                                  ▼
┌──────────────────┐           ┌───────────────────────────────────┐
│   AppSettings    │           │     InvoiceImportService           │
│ Glm.Verified     │           │  - 捕获GLM异常                     │
│ Providers: []    │           │  - 调用 TryGetNextProvider         │
└──────────────────┘           │  - 更新 AppSettings.Glm.Provider   │
                               │  - 触发 StatusChanged 事件通知UI   │
                               └───────────┬───────────────────────┘
                                           │
                                           ▼
                               ┌───────────────────────────────────┐
                               │         GlmService                 │
                               │  (无需修改，保持现有逻辑)          │
                               └───────────────────────────────────┘
```

## 组件规格

### 1. AppSettings.GlmSettings（修改）

```csharp
public class GlmSettings
{
    // 新增属性
    public List<string> VerifiedProviders { get; set; } = [];
}
```

- **用途**：持久化记录用户在设置页面测试通过的提供商
- **值示例**：`["zhipu", "nvidia"]`

### 2. IProviderFallbackManager（新增接口）

```csharp
public interface IProviderFallbackManager
{
    /// <summary>标记某个提供商已通过连接测试</summary>
    Task MarkProviderVerifiedAsync(string provider);

    /// <summary>
    /// 尝试获取下一个可用的提供商。
    /// 返回 null 表示没有更多可用的提供商。
    /// </summary>
    /// <param name="currentProvider">当前失败的提供商</param>
    /// <param name="reason">失败原因（用于日志）</param>
    /// <returns>下一个可用的提供商 ID，或 null</returns>
    string? TryGetNextProvider(string currentProvider, string reason);

    /// <summary>获取已验证的提供商列表</summary>
    IReadOnlyList<string> GetVerifiedProviders();

    /// <summary>重置当前提供商为设置中的默认值</summary>
    void Reset();
}
```

### 3. ProviderFallbackManager（新增实现）

```csharp
public class ProviderFallbackManager : IProviderFallbackManager
{
    private readonly IAppSettingsService _settingsService;

    public ProviderFallbackManager(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task MarkProviderVerifiedAsync(string provider)
    {
        var verified = _settingsService.Settings.Glm.VerifiedProviders;
        if (!verified.Contains(provider))
        {
            verified.Add(provider);
            await _settingsService.SaveAsync();
        }
    }

    public string? TryGetNextProvider(string currentProvider, string reason)
    {
        var verified = _settingsService.Settings.Glm.VerifiedProviders;
        // 记录日志
        LogHelper.Log($"Provider fallback: {currentProvider} failed ({reason})");
        LogHelper.Log($"Verified providers: [{string.Join(", ", verified)}]");

        var currentIndex = verified.IndexOf(currentProvider);
        // 从当前索引之后找下一个
        for (int i = currentIndex + 1; i < verified.Count; i++)
        {
            var next = verified[i];
            LogHelper.Log($"Switching to provider: {next}");
            return next;
        }
        LogHelper.Log($"No more verified providers available");
        return null;
    }

    public IReadOnlyList<string> GetVerifiedProviders()
    {
        return _settingsService.Settings.Glm.VerifiedProviders.AsReadOnly();
    }

    public void Reset()
    {
        // 恢复为设置中保存的提供商
        // 由 InvoiceImportService 在导入开始/结束时调用
    }
}
```

### 4. InvoiceImportService（修改）

#### 新增依赖和事件

```csharp
public interface IInvoiceImportService
{
    // 新增事件
    event EventHandler<string>? StatusChanged;

    Task<ImportResult> ImportAsync(IEnumerable<string> filePaths, Action<int, int>? onProgress = null);
}

public class InvoiceImportService : IInvoiceImportService
{
    private readonly IProviderFallbackManager _fallbackManager;

    public event EventHandler<string>? StatusChanged;

    // 构造函数新增参数
    public InvoiceImportService(
        IBaiduOcrService ocrService,
        IGlmService glmService,
        IInvoiceService invoiceService,
        IFileService fileService,
        IAppSettingsService settingsService,
        IProviderFallbackManager fallbackManager) // 新增
    {
        // ...
        _fallbackManager = fallbackManager;
    }

    private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}
```

#### ProcessBatchAsync 修改逻辑

```csharp
private async Task ProcessBatchAsync(List<ImageItem> batchItems, ImportResult result)
{
    // Phase 3: GLM AI — 添加重试和切换逻辑
    var ocrTexts = ocrSuccessIndices.Select(i => ocrResults[i].Text).ToArray();
    List<GlmInvoiceResponse> glmResults;

    var currentProvider = _settingsService.Settings.Glm.Provider;
    var switchedProvider = false;

    try
    {
        glmResults = await _glmService.ProcessBatchAsync(ocrTexts);
        // 验证返回结果是否有效
        if (glmResults == null || glmResults.Count == 0)
            throw new InvalidOperationException("GLM 返回结果为空");

        // 检查业务逻辑错误（如所有结果都标记为MissingFields）
        var allFailed = glmResults.All(r => r.MissingFields?.Count > 5);
        if (allFailed)
            throw new InvalidOperationException("GLM 返回结果全部标记为缺失字段");
    }
    catch (Exception ex)
    {
        // 尝试切换到下一个提供商
        var nextProvider = _fallbackManager.TryGetNextProvider(currentProvider, ex.Message);
        if (nextProvider != null)
        {
            // 临时切换
            _settingsService.Settings.Glm.Provider = nextProvider;
            switchedProvider = true;
            OnStatusChanged($"当前提供商连接失败，已切换到 {GetProviderName(nextProvider)} 继续处理");

            // 重试
            try
            {
                glmResults = await _glmService.ProcessBatchAsync(ocrTexts);
            }
            catch (Exception retryEx)
            {
                // 新提供商也失败，继续切换或放弃
                HandleFallbackFailure(currentProvider, batchItems, result, retryEx);
                return;
            }
        }
        else
        {
            // 无更多可用提供商
            HandleFallbackFailure(currentProvider, batchItems, result, ex);
            return;
        }
    }

    // ... Phase 4: Save and Archive (原有逻辑)

    // 导入结束后恢复原提供商（由 Import 方法负责）
}
```

#### 新增辅助方法

```csharp
private void HandleFallbackFailure(
    string currentProvider,
    List<ImageItem> batchItems,
    ImportResult result,
    Exception ex)
{
    foreach (var item in batchItems)
    {
        result.Errors.Add($"AI 分析失败（所有可用提供商均已失败）: {item.FilePath}");
    }
    OnStatusChanged("所有已验证提供商均失败，请检查网络连接或提供商设置");
}

private string GetProviderName(string providerId) => providerId switch
{
    "zhipu" => "智谱 (Zhipu)",
    "nvidia" => "NVIDIA NIM",
    "cerebras" => "Cerebras",
    _ => providerId
};
```

#### Import 方法修改（恢复原提供商）

```csharp
public async Task<ImportResult> ImportAsync(...)
{
    var originalProvider = _settingsService.Settings.Glm.Provider;
    try
    {
        // 原有导入逻辑...
    }
    finally
    {
        // 恢复原提供商（不影响设置页面）
        _settingsService.Settings.Glm.Provider = originalProvider;
        _fallbackManager.Reset();
    }
}
```

### 5. SettingsViewModel（修改）

```csharp
// 新增依赖
private readonly IProviderFallbackManager _fallbackManager;

public SettingsViewModel(
    IAppSettingsService settingsService,
    IBaiduOcrService? ocrService = null,
    IGlmService? glmService = null,
    HttpClient? httpClient = null,
    IProviderFallbackManager? fallbackManager = null) // 新增
{
    // ...
    _fallbackManager = fallbackManager;
}

// TestGlmConnectionAsync 成功后
[RelayCommand]
private async Task TestGlmConnectionAsync()
{
    // ... 原有测试逻辑 ...
    if (response.StatusCode == System.Net.HttpStatusCode.OK)
    {
        TestResult = $"{providerName} 连接成功";
        // 新增：标记为已验证
        await _fallbackManager.MarkProviderVerifiedAsync(GlmProvider);
    }
    // ...
}
```

### 6. ImportViewModel（修改）

```csharp
public ImportViewModel(
    IInvoiceImportService importService,
    IFileService fileService)
{
    _importService = importService;
    _fileService = fileService;

    // 订阅状态变更事件（通过接口）
    _importService.StatusChanged += OnImportStatusChanged;
}

private void OnImportStatusChanged(object? sender, string message)
{
    // 确保在UI线程更新
    MainThread.BeginInvokeOnMainThread(() => StatusMessage = message);
}
```

### 7. DI 注册（App.xaml.cs 或相应位置）

```csharp
services.AddSingleton<IProviderFallbackManager, ProviderFallbackManager>();
```

## 状态机

```
[导入开始] → 使用当前 Provider
               ↓
         [GLM 调用成功] → 继续处理
               ↓
         [GLM 调用失败] → TryGetNextProvider
               ↓
         ┌─── 有下一个? ──── 是 ───→ 切换 Provider → 重试
         │                              ↓
         │                         [成功] → 继续后续批次
         │                              ↓
         │                         [失败] → 继续切换
         │
         └─── 否 ───→ 记录错误，跳过该批次
```

## 测试策略

1. **单元测试**: ProviderFallbackManager
   - MarkProviderVerified 去重
   - TryGetNextProvider 按顺序返回下一个
   - 无更多可用提供商时返回 null

2. **集成测试**: 模拟 GLM 失败场景
   - 当前提供商失败 → 切换到下一个 → 重试成功
   - 所有提供商都失败 → 记录错误

3. **UI 测试**: 设置页面测试连接 → VerifiedProviders 更新

## 风险和约束

- **线程安全**: Import 是单线程批次处理，暂不考虑并发
- **429限流**: 不触发切换（现有重试机制已处理）
- **持久化**: VerifiedProviders 随 AppSettings 保存到 appsettings.json
- **临时切换**: 导入结束后恢复原提供商，不影响设置页面
