# 命令行测试模式 - 设计文档

**日期**: 2026-04-09
**状态**: 已批准

---

## 1. 需求概述

### 1.1 核心需求

- 应用支持 `--test` / `-t` 命令行参数进入测试模式
- 支持 `--case=功能名称` 指定单个测试场景
- 支持 `--all` 或不带 case 时运行所有测试
- 测试模式不弹出主窗口，直接执行核心业务逻辑
- 每个测试按标准格式输出结果到控制台
- 测试完成后返回退出码（0=全部通过，非0=有失败）

### 1.2 测试覆盖的功能

| # | 功能 | --case 值 |
|---|------|-----------|
| 1 | 数据加载 | `load` |
| 2 | 分类管理 | `category` |
| 3 | 发票搜索 | `search` |
| 4 | 发票导入 | `import` |
| 5 | 发票导出 | `export` |
| 6 | 发票删除 | `delete` |
| 7 | 已保存列表 | `saved` |
| 8 | 编辑保存 | `edit` |

---

## 2. 架构设计

### 2.1 无头执行流程

```
dotnet run --project src/InvoiceAI.App -- --test --case=load
    ↓
Platforms/Windows/App.xaml.cs (Windows 启动入口)
    ↓
解析命令行参数
    ↓
检测到 --test → 进入无头模式
    ↓
不创建 MAUI 窗口 (不调用 base.OnLaunched)
    ↓
手动构建轻量 DI 容器 (仅注册核心服务，跳过 UI 相关)
    ↓
TestRunner.Run(caseName)
    ↓
执行对应测试用例
    ↓
输出结果 → Environment.Exit(exitCode)
```

### 2.2 服务依赖层次

测试模式只需要核心业务服务，不需要 UI 相关依赖：

```
必需服务 (测试模式注册):
├── AppDbContext (SQLite 数据库)
├── IAppSettingsService (设置管理)
├── IFileService (文件处理)
├── IInvoiceService (发票 CRUD)
├── IExcelExportService (Excel 导出)
├── IBaiduOcrService (OCR 识别)
├── IGlmService (AI 分析)
└── HttpClient (网络请求)

跳过服务 (测试模式不注册):
├── 所有 ViewModels (依赖 UI 绑定)
├── 所有 Pages (UI 页面)
└── MAUI 字体/资源注册
```

### 2.3 测试数据管理

- **使用主数据库**: 测试直接在 `invoices.db` 主数据库上执行
- **测试输入**: 使用 `invoices/` 目录下的真实发票图片作为测试输入
- **数据保护**: 破坏性测试（delete）先创建专用测试数据再删除，确保不留痕迹

---

## 3. 组件设计

### 3.1 TestRunner.cs

核心测试调度器，负责：
1. 解析命令行参数
2. 构建 DI 容器
3. 路由到具体测试用例
4. 格式化输出结果

```csharp
public class TestRunner
{
    // 主入口
    public static async Task<int> RunAsync(string[] args)
    
    // 参数解析
    // --test / -t → 测试模式
    // --case=X → 单个测试
    // --all → 所有测试
    
    // 测试路由
    private static async Task<TestCaseResult> RunTestCase(string caseName, IServiceProvider services)
    
    // 结果格式化
    private static void PrintResult(TestCaseResult result)
}

public record TestCaseResult(
    string CaseName,
    string Input,
    string Expected,
    string Actual,
    string? ScreenshotPath,
    bool Passed,
    string? FailureReason
);
```

### 3.2 测试用例实现

每个测试用例是一个独立方法，接收 `IServiceProvider` 并返回 `TestCaseResult`。

```csharp
public static class TestCases
{
    public static async Task<TestCaseResult> TestLoad(IServiceProvider services)
    public static async Task<TestCaseResult> TestCategory(IServiceProvider services)
    public static async Task<TestCaseResult> TestSearch(IServiceProvider services)
    public static async Task<TestCaseResult> TestImport(IServiceProvider services)
    public static async Task<TestCaseResult> TestExport(IServiceProvider services)
    public static async Task<TestCaseResult> TestDelete(IServiceProvider services)
    public static async Task<TestCaseResult> TestSaved(IServiceProvider services)
    public static async Task<TestCaseResult> TestEdit(IServiceProvider services)
}
```

---

## 4. 测试用例详细设计

### 4.1 TestLoad - 数据加载

- **输入**: 无（查询所有未确认发票）
- **预期**: 返回发票列表，数量 >= 0
- **实际**: 检查返回的 List<Invoice> 和 IsConfirmed=false 过滤
- **截图**: N/A（控制台测试，无需截图）

### 4.2 TestCategory - 分类管理

- **输入**: 查询分类计数
- **预期**: 返回各分类的发票数量字典
- **实际**: 检查 GetCategoryCountsAsync 返回的非空字典
- **额外**: 按分类筛选验证

### 4.3 TestSearch - 发票搜索

- **输入**: 使用搜索词 "测试"（或从数据库选一个实际发行方名）
- **预期**: 返回匹配的发票列表
- **实际**: 检查搜索结果非空且匹配

### 4.4 TestImport - 发票导入

- **输入**: `invoices/` 目录下的一张真实发票图片
- **预期**: OCR → AI 解析 → 保存到数据库，IsConfirmed=false
- **实际**: 检查数据库中新增一条记录
- **注意**: OCR 和 AI 服务需要真实 API 调用，可能失败

### 4.5 TestExport - 发票导出

- **输入**: 导出所有发票到临时 Excel 文件
- **预期**: 生成有效的 .xlsx 文件
- **实际**: 检查文件存在且大小 > 0

### 4.6 TestDelete - 发票删除

- **输入**: 先创建一条测试发票，然后删除
- **预期**: 删除后 GetByIdAsync 返回 null
- **实际**: 检查数据库记录消失

### 4.7 TestSaved - 已保存列表

- **输入**: 查询已确认发票（IsConfirmed=true）
- **预期**: 返回已确认发票列表
- **实际**: 创建一条已确认发票，查询应返回

### 4.8 TestEdit - 编辑保存

- **输入**: 修改发票的 Category 和 Description
- **预期**: UpdateAsync 后数据库记录更新
- **实际**: 读取修改后的记录验证

---

## 5. 错误处理

| 场景 | 处理方式 |
|------|----------|
| 无效 --case 值 | 列出可用 case，退出码=1 |
| 数据库初始化失败 | 输出错误，退出码=1 |
| 单个测试异常 | 捕获异常，标记 FAIL，继续执行后续 |
| 外部服务不可用 (OCR/AI) | 标记 SKIP（非失败），说明原因 |
| 测试数据清理失败 | 输出警告，不阻塞退出 |

---

## 6. 测试输出格式

```
=== 测试 [load] 开始 ===
输入/条件: 查询未确认发票列表
预期结果: 返回发票列表，所有发票 IsConfirmed=false
实际结果: 获取 15 条未确认发票记录
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

## 7. 实现优先级

| 优先级 | 内容 | 说明 |
|--------|------|------|
| P0 | TestRunner 参数解析 + DI 构建 | 基础设施 |
| P0 | 8 个测试用例实现 | 核心功能 |
| P1 | Windows 平台 OnLaunched 拦截 | 无头入口 |
| P1 | 测试数据清理机制 | 避免污染 |
| P2 | 截图功能 | 可选，MAUI 无头环境受限 |
