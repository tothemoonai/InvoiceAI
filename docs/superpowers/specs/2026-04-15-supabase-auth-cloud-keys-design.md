# Supabase 认证与云端 API Key 管理设计文档

> **创建日期:** 2026-04-15
> **状态:** 设计审查中
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
│  - NvidiaApiKey, NvidiaEndpoint, NvidiaModel                   │
│                                                                 │
│  AuthState (Observable)                                         │
│  - IsAuthenticated, UserEmail, UserGroup, ErrorMessage         │
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
│  - nvidia_apikey, nvidia_endpoint, nvidia_model                │
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
    public string OcrToken { get; set; } = string.Empty;
    public string OcrEndpoint { get; set; } = string.Empty;
    public string NvidiaApiKey { get; set; } = string.Empty;
    public string NvidiaEndpoint { get; set; } = string.Empty;
    public string NvidiaModel { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
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
    public string Source { get; set; } = "local"; // "cloud" or "local"
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
    void ClearCachedKeys();
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

    Task<EffectiveApiKeys> GetEffectiveApiKeysAsync();
}
```

---

## 5. 数据流与优先级逻辑

### 5.1 API Key 优先级流程

```
┌─────────────────────────────────────────────────────────────┐
│  服务需要 API Key 时（如 GlmService）                        │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  IAppSettingsService.GetEffectiveApiKeysAsync()             │
│                                                             │
│  1. 检查用户是否已登录 + 已分配云端 Keys                    │
│  │   └─ AuthState.IsAuthenticated && CloudKeys != null    │
│  │                                                          │
│  2. 如果是 → 返回云端 Keys                                  │
│  │                                                          │
│  3. 否则 → 返回本地配置的 Keys                              │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  返回结果：EffectiveApiKeys                                  │
│  {                                                           │
│    OcrToken, OcrEndpoint,                                   │
│    GlmApiKey, GlmEndpoint, GlmModel,                        │
│    Source: "cloud" | "local"                                │
│  }                                                          │
└─────────────────────────────────────────────────────────────┘
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
│  ║  │ ✅ 云端 API Key 已激活                                  ║ │
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
| 获取云端 Key 失败 | 标记为已登录但云端 Key 不可用，fallback 到本地 |
| Supabase Session 过期 | 自动刷新 token，失败则提示重新登录 |

### 7.2 边界情况

| 场景 | 处理方式 |
|------|---------|
| 用户登录成功但 `raw_user_meta_data` 中无 `group_id` | Toast 警告"用户组未配置，使用本地 API Key" |
| 用户组在 `keys` 表中不存在 | Toast 警告"云端 API Key 未配置，使用本地配置" |
| 用户登录时云端 Key 已被管理员删除 | 标记云端 Key 不可用，使用本地 |
| 用户没有配置本地 Key，云端也不可用 | 使用服务时报错（保持原有行为） |

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

---

## 8. Supabase 数据库设置

### 8.1 创建 `public.keys` 表

```sql
-- 在 Supabase SQL Editor 中执行
CREATE TABLE public.keys (
    group_id TEXT PRIMARY KEY,
    ocr_token TEXT NOT NULL,
    ocr_endpoint TEXT NOT NULL,
    nvidia_apikey TEXT NOT NULL,
    nvidia_endpoint TEXT NOT NULL,
    nvidia_model TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 插入测试数据
INSERT INTO public.keys (group_id, ocr_token, ocr_endpoint, nvidia_apikey, nvidia_endpoint, nvidia_model)
VALUES (
    'test_group_1',
    'test_ocr_token',
    'https://aistudio.baidu.com/test',
    'test_nvidia_key',
    'https://integrate.api.nvidia.com/v1/chat/completions',
    'deepseek-ai/deepseek-v3.1-terminus'
);

-- 启用 RLS（可选，根据安全需求）
ALTER TABLE public.keys ENABLE ROW LEVEL SECURITY;

-- 允许认证用户读取 keys
CREATE POLICY "Authenticated users can view keys"
    ON public.keys FOR SELECT
    TO authenticated
    USING (true);
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

- `SignInAsync_Success_ReturnsAuthResultWithEmail`
- `SignInAsync_InvalidPassword_ReturnsFailure`
- `GetCloudKeysAsync_ValidGroupId_ReturnsKeyConfig`
- `GetEffectiveApiKeysAsync_UserLoggedInAndCloudKeysAvailable_ReturnsCloudKeys`
- `LoginCommand_ValidCredentials_CallsAuthService`

---

## 11. 实施任务清单

| 阶段 | 任务 | 预计工作量 |
|------|------|-----------|
| 1 | 创建 Supabase 项目和数据库表 | 30min |
| 2 | 添加 Models 层 (Auth/*.cs) | 1h |
| 3 | 添加 Services 接口和实现 | 2h |
| 4 | 扩展 IAppSettingsService | 30min |
| 5 | 创建 AuthViewModel | 1h |
| 6 | 修改 SettingsPage UI | 1h |
| 7 | 修改 GlmService/BaiduOcrService 使用新的 Key 获取方式 | 1h |
| 8 | 编写单元测试 | 2h |
| 9 | 集成测试和调试 | 2h |
| **总计** | | **~10h** |

---

## 12. 设计决策

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
│       └── SupabaseConfig.cs
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
