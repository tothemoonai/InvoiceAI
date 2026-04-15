# Supabase 认证与云端 API Key 管理设计文档

> **创建日期:** 2026-04-15
> **状态:** 设计审查中 (v2 - 根据反馈修订)
> **作者:** Claude Code
> **项目:** InvoiceAI

---

## 1. 概述

### 1.1 目标

为 InvoiceAI 应用添加基于 Supabase 的用户认证功能，允许用户登录后自动获取云端配置的 OCR 和 NVIDIA API Key，无需手动配置。云端 Key 优先于本地配置使用，但保持两套系统独立运行。

### 1.2 关键需求

- 用户使用邮箱 + 密码登录 Supabase
- 登录后根据用户组 (`group_id`) 获取云端 API Key
- 云端 Key 对用户不可见，仅用于后台调用
- 云端 Key 优先于本地配置使用
- 用户不登录也可使用应用（使用本地配置的 Key）
- 登录状态保持 30 天
- 登录界面集成在 Settings 页面顶部

---

## 2. 架构设计

### 2.1 整体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        InvoiceAI App                            │
├─────────────────────────────────────────────────────────────────┤
│  Settings Page (顶部新增账户区域)                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ 📦 账户区域                                              │   │
│  │ • 未登录: 显示"登录后可使用云端 API Key" + 登录按钮       │   │
│  │ • 已登录: 显示用户邮箱 + 用户组 + 登出按钮                │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  原有设置区域（本地 API Key 配置，保持不变）                    │
└─────────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Core Layer (新增)                            │
├─────────────────────────────────────────────────────────────────┤
│  IAuthService / SupabaseAuthService                            │
│  • SignInAsync(email, password) → AuthResult                   │
│  • SignOutAsync() → void                                       │
│  • GetCurrentUserAsync() → User?                               │
│  • GetCurrentUserGroupAsync() → string? (group_id)             │
│                                                                 │
│  ICloudKeyService / SupabaseKeyService                         │
│  • GetCloudKeysAsync(groupId) → CloudKeyConfig?                │
│  • GetCachedCloudKeysAsync() → CloudKeyConfig? (thread-safe)   │
│  • IsCloudKeyValid(config) → bool                              │
│  • ClearCachedKeys()                                           │
│  • 内部使用 lock 保护缓存访问                                    │
└─────────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Models Layer (新增)                          │
├─────────────────────────────────────────────────────────────────┤
│  User (Supabase Auth)                                           │
│  - Id, Email, RawUserMetaData (contains group_id)             │
│                                                                 │
│  CloudKeyConfig                                                │
│  - OcrToken, OcrEndpoint                                       │
│  - Zhipu/Nvidia/Cerebras ApiKey, Endpoint, Model (all providers)│
│  - Version (for key rotation)                                  │
│                                                                 │
│  AuthState (Observable, thread-safe)                            │
│  - IsAuthenticated, UserEmail, UserGroup, ErrorMessage         │
│  - CloudKeysAvailable, ActiveCloudProvider, IsUsingCloudKeys    │
└─────────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Supabase Backend                             │
├─────────────────────────────────────────────────────────────────┤
│  auth.users (Supabase Auth)                                     │
│  - raw_user_meta_data: { "group_id": "premium" }               │
│                                                                 │
│  public.keys (新建)                                             │
│  - group_id (PK)                                               │
│  - ocr_token, ocr_endpoint                                     │
│  - zhipu_apikey, zhipu_endpoint, zhipu_model                   │
│  - nvidia_apikey, nvidia_endpoint, nvidia_model                │
│  - cerebras_apikey, cerebras_endpoint, cerebras_model          │
│  - 至少一个 GLM 提供商必须配置                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 分层说明

**App Layer:**
- `SettingsPage.cs` - 新增账户区域 UI
- `AuthViewModel.cs` - 管理登录/登出状态和操作

**Core Layer:**
- `IAuthService` / `SupabaseAuthService` - Supabase 认证封装
- `ICloudKeyService` / `SupabaseKeyService` - 云端 Key 获取服务
- `IAppSettingsService` - 扩展 `GetEffectiveApiKeysAsync()` 方法

**Models Layer:**
- `CloudKeyConfig` - 云端 Key 配置模型
- `EffectiveApiKeys` - 有效 API Key 模型（带来源标识）
- `AuthState` - 认证状态模型
- `AuthResult` - 认证结果模型

---

## 3. 数据模型

### 3.1 新增 Models

```csharp
// src/InvoiceAI.Models/Auth/CloudKeyConfig.cs
namespace InvoiceAI.Models.Auth;

public class CloudKeyConfig
{
    // OCR
    public string OcrToken { get; set; } = string.Empty;
    public string OcrEndpoint { get; set; } = string.Empty;

    // GLM - Support all providers
    public string? ZhipuApiKey { get; set; }
    public string? ZhipuEndpoint { get; set; }
    public string? ZhipuModel { get; set; }

    public string? NvidiaApiKey { get; set; }
    public string? NvidiaEndpoint { get; set; }
    public string? NvidiaModel { get; set; }

    public string? CerebrasApiKey { get; set; }
    public string? CerebrasEndpoint { get; set; }
    public string? CerebrasModel { get; set; }

    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1; // For key rotation support
}

// src/InvoiceAI.Models/Auth/EffectiveApiKeys.cs
namespace InvoiceAI.Models.Auth;

public class EffectiveApiKeys
{
    public string OcrToken { get; set; } = string.Empty;
    public string OcrEndpoint { get; set; } = string.Empty;
    public string GlmApiKey { get; set; } = string.Empty;
    public string GlmEndpoint { get; set; } = string.Empty;
    public string GlmModel { get; set; } = string.Empty;
    public string GlmProvider { get; set; } = "zhipu"; // "zhipu" | "nvidia" | "cerebras"
    public string Source { get; set; } = "local"; // "cloud" or "local"
    public int KeyVersion { get; set; } = 1; // For tracking key rotation
}

// src/InvoiceAI.Models/Auth/AuthState.cs
namespace InvoiceAI.Models.Auth;

public class AuthState
{
    public bool IsAuthenticated { get; set; }
    public string? UserEmail { get; set; }
    public string? UserGroup { get; set; }
    public string? ErrorMessage { get; set; }
    public bool CloudKeysAvailable { get; set; }
    public string? ActiveCloudProvider { get; set; } // "zhipu" | "nvidia" | "cerebras" | null
    public bool IsUsingCloudKeys { get; set; } // Currently using cloud keys
}

// src/InvoiceAI.Models/Auth/AuthResult.cs
namespace InvoiceAI.Models.Auth;

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UserEmail { get; set; }
    public string? UserGroup { get; set; }
}
```

### 3.2 扩展现有 Models

```csharp
// src/InvoiceAI.Core/Helpers/AppSettings.cs
public class AppSettings
{
    // ... 现有属性 ...

    // 新增：Supabase 配置
    public SupabaseConfig Supabase { get; set; } = new();
}

public class SupabaseConfig
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
}
```

---

## 4. 服务接口与实现

### 4.1 IAuthService

```csharp
// src/InvoiceAI.Core/Services/IAuthService.cs
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string email, string password);
    Task SignOutAsync();
    Task<AuthState> GetAuthStateAsync();
    Task<string?> GetCurrentUserGroupAsync();
    event EventHandler<AuthState>? AuthStateChanged;
}
```

### 4.2 ICloudKeyService

```csharp
// src/InvoiceAI.Core/Services/ICloudKeyService.cs
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public interface ICloudKeyService
{
    Task<CloudKeyConfig?> GetCloudKeysAsync(string groupId);
    Task<CloudKeyConfig?> GetCachedCloudKeysAsync(); // Thread-safe cache access
    void ClearCachedKeys();
    bool IsCloudKeyValid(CloudKeyConfig? config); // Validate cloud keys
}
```

### 4.3 IAppSettingsService 扩展

```csharp
// src/InvoiceAI.Core/Services/IAppSettingsService.cs (新增方法)
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public interface IAppSettingsService
{
    // ... 现有方法 ...

    /// <summary>
    /// 获取有效的 API Keys，优先使用云端配置，fallback 到本地配置。
    /// 方法是线程安全的，可以被多个服务并发调用。
    /// </summary>
    /// <returns>有效的 API Keys 配置，包含来源标识 (cloud/local)</returns>
    Task<EffectiveApiKeys> GetEffectiveApiKeysAsync();
}
```

---

## 5. 数据流与优先级逻辑

### 5.1 API Key 优先级流程（带错误处理和线程安全）

```
┌─────────────────────────────────────────────────────────────┐
│  服务需要 API Key 时（如 GlmService）                        │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  IAppSettingsService.GetEffectiveApiKeysAsync()             │
│  [线程安全: 使用 SemaphoreSlim 保护并发访问]                 │
│                                                             │
│  1. 检查用户是否已登录 + 云端 Keys 可用且有效                │
│  │   └─ AuthState.IsAuthenticated && CloudKeysAvailable    │
│  │       && IsCloudKeyValid(cachedKeys)                    │
│  │                                                          │
│  2. 如果云端 Keys 有效 → 返回云端 Keys                      │
│  │   └─ 返回 EffectiveApiKeys { Source: "cloud", ... }     │
│  │                                                          │
│  3. 否则 → 返回本地配置的 Keys                              │
│  │   └─ 返回 EffectiveApiKeys { Source: "local", ... }    │
│  │                                                          │
│  [异常处理: 所有异常被捕获并记录，fallback 到本地 Keys]      │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  返回结果：EffectiveApiKeys                                  │
│  {                                                           │
│    OcrToken, OcrEndpoint,                                   │
│    GlmApiKey, GlmEndpoint, GlmModel, GlmProvider,           │
│    Source: "cloud" | "local",                               │
│    KeyVersion: 1                                            │
│  }                                                          │
└─────────────────────────────────────────────────────────────┘
```

**线程安全保证：**
- `SupabaseKeyService` 内部使用 `lock` 保护缓存访问
- `AuthState` 更新使用 `SemaphoreSlim` 防止并发修改
- `GetEffectiveApiKeysAsync` 可被多个服务并发调用

**Key 有效性验证：**
```csharp
bool IsCloudKeyValid(CloudKeyConfig? config)
{
    if (config == null) return false;
    // 检查至少有一个 GLM 提供商配置完整
    bool hasValidGlmProvider =
        (!string.IsNullOrEmpty(config.ZhipuApiKey) && !string.IsNullOrEmpty(config.ZhipuModel)) ||
        (!string.IsNullOrEmpty(config.NvidiaApiKey) && !string.IsNullOrEmpty(config.NvidiaModel)) ||
        (!string.IsNullOrEmpty(config.CerebrasApiKey) && !string.IsNullOrEmpty(config.CerebrasModel));
    // 检查 OCR 配置完整
    bool hasValidOcr = !string.IsNullOrEmpty(config.OcrToken) &&
                       !string.IsNullOrEmpty(config.OcrEndpoint);
    return hasValidGlmProvider && hasValidOcr;
}
```

### 5.2 登录流程

```
用户点击"登录"按钮
       │
       ▼
弹出登录对话框 (Entry: Email, Password)
       │
       ▼
用户输入并确认
       │
       ▼
AuthViewModel.LoginCommand
       │
       ├─► SupabaseAuthService.SignInAsync(email, password)
       │       │
       │       ├─► 成功 → 获取 user.raw_user_meta_data.group_id
       │       │       │
       │       │       ▼
       │       │   SupabaseKeyService.GetCloudKeysAsync(groupId)
       │       │       │
       │       │       ├─► 成功 → 缓存 CloudKeyConfig 到内存
       │       │       │       │
       │       │       │       ▼
       │       │       │   更新 AuthState (IsAuthenticated=true)
       │       │       │       │
       │       │       │       ▼
       │       │       │   通知 UI 刷新（显示用户信息）
       │       │       │
       │       └─► 失败 → AuthState.ErrorMessage = error
       │
       └─► UI 显示结果
```

### 5.3 登出流程

```
用户点击"登出"按钮
       │
       ▼
AuthViewModel.LogoutCommand
       │
       ├─► SupabaseAuthService.SignOutAsync()
       │       │
       │       ├─► 清除本地 Session
       │       │
       │       └─► 清除 CloudKeyConfig 内存缓存
       │
       ├─► 重置 AuthState (IsAuthenticated=false)
       │
       └─► 通知 UI 刷新（显示登录提示）
```

### 5.4 Key 优先级示例

| 场景 | 用户登录状态 | 云端 Key 可用 | 本地 Key 已配置 | 实际使用 |
|------|------------|-------------|---------------|---------|
| 1 | ❌ 未登录 | N/A | ✅ 是 | 本地 |
| 2 | ✅ 已登录 | ✅ 是 | ✅ 是 | **云端** |
| 3 | ✅ 已登录 | ✅ 是 | ❌ 否 | **云端** |
| 4 | ✅ 已登录 | ❌ 否 | ✅ 是 | 本地（fallback） |
| 5 | ✅ 已登录 | ❌ 否 | ❌ 否 | 报错 |

---

## 6. UI 设计

### 6.1 Settings 页面新布局

```
┌─────────────────────────────────────────────────────────────────┐
│  设置                                                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ╔════════════════════════════════════════════════════════════╗ │
│  ║ 📦 账户                                     [状态标识]      ║ │
│  ╠════════════════════════════════════════════════════════════╣ │
│  ║                                                              ║ │
│  ║  [未登录状态显示]                                            ║ │
│  ║  ┌────────────────────────────────────────────────────────┐ ║ │
│  ║  │ 登录后可使用云端 API Key，无需手动配置                  │ ║ │
│  ║  │ 云端 Key 优先于本地配置使用                             │ ║ │
│  ║  │                                                         │ ║ │
│  ║  │                              [登录...] 按钮             ║ │
│  ║  └────────────────────────────────────────────────────────┘ ║ │
│  ║                                                              ║ │
│  ║  [已登录状态显示]                                            ║ │
│  ║  ┌────────────────────────────────────────────────────────┐ ║ │
│  ║  │ 👤 user@example.com                                     ║ │
│  ║  │ 🏷️ 用户组: Premium                                      ║ │
│  ║  │ ✅ 云端 API Key 已激活 (NVIDIA NIM)                      ║ │
│  ║  │ 🔄 当前使用: 云端配置                                   ║ │
│  ║  │                                                         │ ║ │
│  ║  │                              [登出] 按钮                ║ │
│  ║  └────────────────────────────────────────────────────────┘ ║ │
│  ║                                                              ║ │
│  ╚════════════════════════════════════════════════════════════╝ │
│                                                                  │
│  ╔════════════════════════════════════════════════════════════╗ │
│  ║ PaddleOCR 设置                                              ║ │
│  ╠════════════════════════════════════════════════════════════╣ │
│  ║ Token: [________]                                           ║ │
│  ║ 端点: [________]              [测试 OCR 连接]               ║ │
│  ╚════════════════════════════════════════════════════════════╝ │
│                                                                  │
│  ╔════════════════════════════════════════════════════════════╗ │
│  ║ LLM API 设置                                                ║ │
│  ╠════════════════════════════════════════════════════════════╣ │
│  ║ ... (原有内容保持不变)                                      ║ │
│  ╚════════════════════════════════════════════════════════════╝ │
│                                                                  │
│  ... (其他设置区域保持不变)                                     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 登录对话框

```
┌─────────────────────────────────────────────┐
│  用户登录                        [×]        │
├─────────────────────────────────────────────┤
│                                             │
│  电子邮箱                                   │
│  ┌───────────────────────────────────────┐ │
│  │ user@example.com                      │ │
│  └───────────────────────────────────────┘ │
│                                             │
│  密码                                       │
│  ┌───────────────────────────────────────┐ │
│  │ ••••••••                               │ │
│  └───────────────────────────────────────┘ │
│                                             │
│  ┌─────────────┐  ┌─────────────┐         │
│  │  取消        │  │  登录        │         │
│  └─────────────┘  └─────────────┘         │
│                                             │
└─────────────────────────────────────────────┘
```

---

## 7. 错误处理与边界情况

### 7.1 网络错误处理

| 场景 | 处理方式 |
|------|---------|
| Supabase 连接超时 | Toast 提示"网络连接失败，请检查网络" |
| 登录失败（密码错误） | 对话框内显示红色错误信息 |
| 获取云端 Key 失败 | Toast 警告"云端 Key 获取失败，使用本地配置" |
| 云端 Key 验证失败 | 标记为已登录但云端 Key 不可用，fallback 到本地 |
| Session 过期（自动刷新失败） | Toast 提示"登录已过期，请重新登录"，清除认证状态 |
| 用户在服务调用中途登出 | 正在执行的服务调用完成，下次调用使用本地 Keys |
| 并发服务调用时认证状态变化 | SemaphoreSlim 保护状态切换，等待完成后再切换 |

### 7.2 边界情况

| 场景 | 处理方式 |
|------|---------|
| 用户登录成功但 `raw_user_meta_data` 中无 `group_id` | Toast 警告"用户组未配置，使用本地 API Key" |
| 用户组在 `keys` 表中不存在 | Toast 警告"云端 API Key 未配置，使用本地配置" |
| 用户登录时云端 Key 已被管理员删除 | 标记云端 Key 不可用，使用本地 |
| 云端 Key 部分配置（只有 OCR，没有 GLM） | 标记云端 Key 不可用，使用本地 |
| 用户没有配置本地 Key，云端也不可用 | 使用服务时报错（保持原有行为） |
| 启动时网络不可用但 Session 存在 | 标记为"离线模式"，使用本地 Keys 或已缓存的云端 Keys |
| 云端 Key 已过期（version 不匹配） | 后台异步刷新，期间使用已缓存的 Keys |

### 7.3 启动流程

```
App 启动
    │
    ▼
加载本地配置 (appsettings.json)
    │
    ▼
尝试恢复 Supabase Session (从本地持久化)
    │
    ├─► Session 有效
    │       │
    │       ├─► 获取用户信息
    │       │       │
    │       │       ▼
    │       │   获取 group_id
    │       │       │
    │       │       ▼
    │       │   加载云端 Key（后台异步）
    │       │       │
    │       │       ├─► 成功 → 标记已登录 + 云端 Key 可用
    │       │       └─► 失败 → 标记已登录但使用本地 Key
    │       │
    │       ▼
    │   UI 显示已登录状态
    │
    └─► Session 无效/不存在
            │
            ▼
        UI 显示未登录状态
```

### 7.4 Session 持久化与刷新策略

**Session 存储：**
- Supabase SDK 默认使用本地持久化存储（`FileSystem.Current.AppDataDirectory/supabase_auth.json`）
- Access Token 和 Refresh Token 加密存储（使用 DPAPI on Windows）
- Session 有效期：30 天（通过 Supabase Dashboard 配置）

**Session 刷新流程：**
```
┌─────────────────────────────────────────────────────────────┐
│  API 调用检测到 401 Unauthorized                             │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  SupabaseAuthService 尝试自动刷新 Token                     │
│  │                                                          │
│  ├─► 使用 Refresh Token 调用 Supabase Auth API             │
│  │       │                                                  │
│  │       ├─► 成功 → 更新本地存储的 Token                    │
│  │       │       │                                          │
│  │       │       ▼                                          │
│  │       │   重试原始 API 调用                              │
│  │       │                                                  │
│  │       └─► 失败 (Refresh Token 也过期)                    │
│  │               │                                          │
│  │               ▼                                          │
│  │           清除本地 Session                               │
│  │           触发 AuthStateChanged (IsAuthenticated=false) │
│  │           UI 提示"登录已过期，请重新登录"                 │
│  │                                                          │
│  └─► 最多重试 1 次，避免无限循环                            │
└─────────────────────────────────────────────────────────────┘
```

**离线启动行为：**
- 如果本地 Session 存在但网络不可用：标记为"离线模式"
- 离线模式下不尝试刷新 Session 或获取云端 Keys
- 使用最后一次有效的配置（本地或缓存的云端 Keys）
- 网络恢复后自动尝试重新同步
```

### 7.5 线程安全与并发控制

**共享状态保护：**

| 共享状态 | 保护机制 | 用途 |
|---------|---------|------|
| `CloudKeyConfig` 缓存 | `lock (_cacheLock)` | 防止并发读写缓存 |
| `AuthState` 更新 | `SemaphoreSlim _stateLock` | 防止并发修改认证状态 |
| Session 刷新 | `SemaphoreSlim _refreshLock` | 防止并发刷新 Token |

**并发场景处理：**

```csharp
// SupabaseKeyService 线程安全实现
public class SupabaseKeyService : ICloudKeyService
{
    private CloudKeyConfig? _cachedKeys;
    private readonly object _cacheLock = new();

    public async Task<CloudKeyConfig?> GetCachedCloudKeysAsync()
    {
        lock (_cacheLock)
        {
            return _cachedKeys; // 读操作受 lock 保护
        }
    }

    public void ClearCachedKeys()
    {
        lock (_cacheLock)
        {
            _cachedKeys = null;
        }
    }
}

// AuthState 更新线程安全
public class SupabaseAuthService : IAuthService
{
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    public async Task<AuthState> GetAuthStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            return _currentAuthState;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task UpdateAuthStateAsync(AuthState newState)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentAuthState = newState;
            AuthStateChanged?.Invoke(this, newState);
        }
        finally
        {
            _stateLock.Release();
        }
    }
}
```

**服务间协调：**
- `GlmService` 和 `BaiduOcrService` 可并发调用 `GetEffectiveApiKeysAsync()`
- 认证状态变化时，等待进行中的服务调用完成再切换 Keys
- 登出操作：`SemaphoreSlim.Wait()` 等待所有进行中的调用完成

---

## 8. Supabase 数据库设置

### 8.1 创建 `public.keys` 表

```sql
-- 在 Supabase SQL Editor 中执行
CREATE TABLE public.keys (
    group_id TEXT PRIMARY KEY,

    -- OCR (必需)
    ocr_token TEXT NOT NULL CHECK (ocr_token != ''),
    ocr_endpoint TEXT NOT NULL CHECK (ocr_endpoint != ''),

    -- GLM 提供商 (至少一个必需)
    nvidia_apikey TEXT,
    nvidia_endpoint TEXT,
    nvidia_model TEXT,

    zhipu_apikey TEXT,
    zhipu_endpoint TEXT,
    zhipu_model TEXT,

    cerebras_apikey TEXT,
    cerebras_endpoint TEXT,
    cerebras_model TEXT,

    -- 元数据
    version INT DEFAULT 1,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    -- 约束: 至少一个 GLM 提供商必须配置
    CONSTRAINT check_at_least_one_glm_provider CHECK (
        (nvidia_apikey IS NOT NULL AND nvidia_model IS NOT NULL) OR
        (zhipu_apikey IS NOT NULL AND zhipu_model IS NOT NULL) OR
        (cerebras_apikey IS NOT NULL AND cerebras_model IS NOT NULL)
    )
);

-- 索引
CREATE INDEX idx_keys_group_id ON public.keys(group_id);

-- 审计触发器
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.version = OLD.version + 1;
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_keys_updated_at BEFORE UPDATE ON public.keys
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- 插入测试数据
INSERT INTO public.keys (
    group_id, ocr_token, ocr_endpoint,
    nvidia_apikey, nvidia_endpoint, nvidia_model
)
VALUES (
    'test_group_1',
    'test_ocr_token',
    'https://aistudio.baidu.com/test',
    'test_nvidia_key',
    'https://integrate.api.nvidia.com/v1/chat/completions',
    'deepseek-ai/deepseek-v3.1-terminus'
);

-- 启用 RLS（安全要求）
ALTER TABLE public.keys ENABLE ROW LEVEL SECURITY;

-- 创建用户组关联表（用于 RLS）
CREATE TABLE IF NOT EXISTS public.user_groups (
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    group_id TEXT REFERENCES public.keys(group_id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, group_id)
);

-- RLS 策略: 用户只能访问其所属组的 Keys
CREATE POLICY "Users can view keys for their group"
    ON public.keys FOR SELECT
    TO authenticated
    USING (
        group_id IN (
            SELECT group_id FROM public.user_groups WHERE user_id = auth.uid()
        )
    );

-- RLS 策略: 用户组关联表
ALTER TABLE public.user_groups ENABLE ROW LEVEL SECURITY;
CREATE POLICY "Users can view their own groups"
    ON public.user_groups FOR SELECT
    TO authenticated
    USING (user_id = auth.uid());
```

### 8.2 appsettings.json 配置

```json
{
  "Supabase": {
    "Url": "https://xxx.supabase.co",
    "AnonKey": "eyJxxx..."
  },
  "BaiduOcr": { ... },
  "Glm": { ... },
  ...
}
```

---

## 9. 依赖项

### 9.1 新增 NuGet 包

```xml
<PackageReference Include="Supabase" Version="..." />
<PackageReference Include="Supabase.Gotrue" Version="..." />
```

### 9.2 DI 注册

```csharp
// src/InvoiceAI.App/MauiProgram.cs
builder.Services.AddSingleton<IAuthService, SupabaseAuthService>();
builder.Services.AddSingleton<ICloudKeyService, SupabaseKeyService>();
builder.Services.AddSingleton<AuthViewModel>();
```

---

## 10. 测试策略

### 10.1 新增测试文件

```
tests/InvoiceAI.Core.Tests/
├── Services/
│   ├── SupabaseAuthServiceTests.cs
│   ├── SupabaseKeyServiceTests.cs
│   └── AppSettingsServiceExtensionsTests.cs
└── ViewModels/
    └── AuthViewModelTests.cs
```

### 10.2 核心测试用例

**认证服务测试 (`SupabaseAuthServiceTests`)**
- `SignInAsync_ValidCredentials_ReturnsSuccessWithUserInfo`
- `SignInAsync_InvalidPassword_ReturnsFailure`
- `SignOutAsync_ClearsSessionAndRaisesEvent`
- `GetAuthStateAsync_AfterSignIn_ReturnsAuthenticatedState`
- `SessionExpired_TriggersRefreshOrSignOut`

**云端 Key 服务测试 (`SupabaseKeyServiceTests`)**
- `GetCloudKeysAsync_ValidGroupId_ReturnsCompleteKeyConfig`
- `GetCloudKeysAsync_InvalidGroupId_ReturnsNull`
- `GetCachedCloudKeysAsync_ConcurrentCalls_DoesNotRace`
- `IsCloudKeyValid_CompleteConfig_ReturnsTrue`
- `IsCloudKeyValid_PartialConfig_ReturnsFalse`
- `ClearCachedKeys_RemovesCachedData`

**优先级逻辑测试 (`AppSettingsServiceExtensionsTests`)**
- `GetEffectiveApiKeysAsync_UserLoggedInAndCloudKeysAvailable_ReturnsCloudKeys`
- `GetEffectiveApiKeysAsync_UserNotLoggedIn_ReturnsLocalKeys`
- `GetEffectiveApiKeysAsync_CloudKeysInvalid_ReturnsLocalKeys`
- `GetEffectiveApiKeysAsync_ConcurrentCalls_DoesNotRace`
- `GetEffectiveApiKeysAsync_NetworkFailure_FallsBackToLocal`

**ViewModel 测试 (`AuthViewModelTests`)**
- `LoginCommand_ValidCredentials_CallsAuthServiceAndFetchesKeys`
- `LoginCommand_InvalidCredentials_ShowsError`
- `LogoutCommand_CallsAuthServiceAndClearsCloudKeys`
- `LogoutCommand_WaitsForPendingServiceCalls`

**集成测试**
- `LoginFlow_FullIntegration_ReturnsCloudKeysAndUpdatesUI`
- `LogoutFlow_ClearsAllStateAndUpdatesUI`
- `SessionRefresh_AfterExpiry_LogsUserOut`
```

---

## 11. 安全考虑

### 11.1 云端 Key 存储

**内存加密：**
- 云端 Key 加载到内存后使用 DPAPI (Windows) 加密存储
- Key 在使用时解密，使用后立即清除敏感数据
- 调试模式下不记录完整 Key，只记录前 4 位和后 4 位

**Key 轮换：**
- 数据库中的 `version` 字段用于跟踪 Key 版本
- 当云端 Key 更新时，version 递增
- 应用定期检查 version，如果不匹配则重新获取

### 11.2 RLS 策略

**用户组隔离：**
- 每个用户只能访问其所属组的 Keys
- 通过 `public.user_groups` 表关联用户和组
- RLS 策略确保用户无法绕过限制访问其他组的 Keys

### 11.3 Session 安全

**Token 存储：**
- Access Token 和 Refresh Token 加密存储在本地文件系统
- 使用 `System.Security.Cryptography.DataProtection` API
- Windows 上使用 DPAPI，其他平台使用平台特定加密

**CSRF 保护：**
- Supabase SDK 内置 CSRF 保护
- Desktop 应用上下文中，通过 `Origin` 头验证请求来源

### 11.4 审计日志

**操作日志：**
- 记录所有登录/登出操作
- 记录云端 Key 获取和验证失败
- 记录 Key 版本变化
- 日志存储在本地，不上传（保护隐私）

---

## 13. 实施任务清单

| 阶段 | 任务 | 预计工作量 |
|------|------|-----------|
| 1 | 创建 Supabase 项目和数据库表（含约束和 RLS） | 45min |
| 2 | 添加 Models 层 (Auth/*.cs) - 支持所有 GLM 提供商 | 1.5h |
| 3 | 添加 Services 接口和实现（含线程安全） | 2.5h |
| 4 | 扩展 IAppSettingsService（优先级逻辑 + 错误处理） | 45min |
| 5 | 创建 AuthViewModel | 1h |
| 6 | 修改 SettingsPage UI（账户区域） | 1h |
| 7 | 修改 GlmService/BaiduOcrService 使用新的 Key 获取方式 | 1h |
| 8 | 实现 Session 持久化和刷新机制 | 1h |
| 9 | 实现内存加密和审计日志 | 45min |
| 10 | 编写单元测试（含并发测试） | 2h |
| 11 | 集成测试和调试 | 2h |
| **总计** | | **~13.5h** |

---

## 14. 设计决策

### 12.1 方案选择：最小改动方案

选择方案 A（最小改动）的原因：
- 改动最小，风险最低
- 云端 Key 逻辑封装在独立服务中
- 不影响现有的测试
- 易于后续扩展

### 12.2 两套独立系统

云端 Key 和本地 Key 是两套独立的配置系统：
- Settings 页面始终显示用户本地配置的 Key
- 云端 Key 在后台使用，对用户不可见
- 登录状态不影响本地配置的显示和编辑

### 12.3 用户组存储位置

使用 Supabase Auth 的 `raw_user_meta_data` 存储 `group_id`：
- 利用 Supabase Auth 的完整认证功能
- 不需要额外的表存储 user_profiles
- `raw_user_meta_data` 是 JSONB 字段，灵活存储

### 12.4 多提供商支持

云端 Key 配置支持所有 GLM 提供商（Zhipu、NVIDIA NIM、Cerebras）：
- 每个提供商的 Key 可独立配置
- 至少需要一个提供商的完整配置
- 应用根据当前选中的提供商使用对应的云端 Key

### 12.5 线程安全策略

使用多种同步机制保护共享状态：
- `lock`：简单缓存访问（读写操作快速）
- `SemaphoreSlim`：复杂状态切换（支持异步等待）
- 服务调用期间禁止状态切换，确保一致性

使用 Supabase Auth 的 `raw_user_meta_data` 存储 `group_id`：
- 利用 Supabase Auth 的完整认证功能
- 不需要额外的表存储 user_profiles
- `raw_user_meta_data` 是 JSONB 字段，灵活存储

---

## 附录：文件清单

### 新增文件

```
src/
├── InvoiceAI.Models/
│   └── Auth/
│       ├── CloudKeyConfig.cs
│       ├── EffectiveApiKeys.cs
│       ├── AuthState.cs
│       └── AuthResult.cs
├── InvoiceAI.Core/
│   ├── Services/
│   │   ├── IAuthService.cs
│   │   ├── SupabaseAuthService.cs
│   │   ├── ICloudKeyService.cs
│   │   └── SupabaseKeyService.cs
│   ├── ViewModels/
│   │   └── AuthViewModel.cs
│   └── Helpers/
│       ├── SupabaseConfig.cs
│       ├── DataProtection.cs (内存加密)
│       └── AuthAuditLogger.cs (审计日志)
tests/
└── InvoiceAI.Core.Tests/
    ├── Services/
    │   ├── SupabaseAuthServiceTests.cs
    │   ├── SupabaseKeyServiceTests.cs
    │   └── AppSettingsServiceExtensionsTests.cs
    └── ViewModels/
        └── AuthViewModelTests.cs
```

### 修改文件

```
src/
├── InvoiceAI.Core/
│   ├── Helpers/AppSettings.cs (新增 Supabase 节点)
│   ├── Services/
│   │   ├── IAppSettingsService.cs (新增 GetEffectiveApiKeysAsync)
│   │   ├── AppSettingsService.cs (实现新方法)
│   │   ├── GlmService.cs (使用新的 Key 获取方式)
│   │   └── BaiduOcrService.cs (使用新的 Key 获取方式)
└── InvoiceAI.App/
    ├── MauiProgram.cs (注册新服务)
    └── Pages/SettingsPage.cs (添加账户区域 UI)
```
