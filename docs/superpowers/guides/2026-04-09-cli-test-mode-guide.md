# 命令行测试模式 - 使用指南

> 最后更新: 2026-04-09
> 
> **更新记录**:
> 
> - 测试数据路径改为 `TEMP\testdata\`（项目根目录下），支持 jpg/png/pdf
> - 测试日志输出到 `TEMP\testlog\` 目录
> - 移除确认相关测试，保留 7 个核心测试用例
> - 使用相对路径自动查找项目根目录，不依赖绝对路径

## 目录

1. [概述](#1-概述)
2. [快速开始](#2-快速开始)
3. [参数说明](#3-参数说明)
4. [测试用例详解](#4-测试用例详解)
5. [输出格式说明](#5-输出格式说明)
6. [常见场景示例](#6-常见场景示例)
7. [测试结果解读](#7-测试结果解读)
8. [故障排查](#8-故障排查)
9. [如何添加新测试用例](#9-如何添加新测试用例)
10. [架构原理](#10-架构原理)
11. [最佳实践](#11-最佳实践)

---

## 1. 概述

命令行测试模式是 InvoiceAI 内置的一种无头（Headless）运行模式，允许在 **不启动 GUI 窗口** 的情况下，通过命令行直接执行核心业务逻辑测试。

### 设计目的

| 目的           | 说明                   |
| ------------ | -------------------- |
| **快速验证**     | 不启动窗口即可验证核心功能是否正常    |
| **CI/CD 集成** | 可集成到自动化流水线中          |
| **调试辅助**     | 通过控制台输出快速定位问题        |
| **回归测试**     | 每次修改后运行测试，确保已有功能不被破坏 |

### 技术特点

- **无头模式**: 不创建 MAUI 窗口，跳过 UI 初始化
- **直接数据库**: 在 `invoices.db` 主数据库上执行
- **轻量 DI**: 仅注册核心业务服务，跳过 UI 相关依赖
- **安全清理**: 破坏性测试（删除/编辑）创建专用测试数据并自动清理

---

## 2. 快速开始

### 基本命令

```bash
# 运行所有测试
"InvoiceAI.App.exe" --test --all

# 运行单个测试
"InvoiceAI.App.exe" --test --case=load

# 简写形式
"InvoiceAI.App.exe" -t --case=delete
```

### 编译后运行

```bash
# 先编译
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj

# 运行（使用编译后的 exe）
"src/InvoiceAI.App/bin/Debug/net9.0-windows10.0.19041.0/win10-x64/InvoiceAI.App.exe" --test --all
```

### 输出重定向

测试输出默认输出到控制台。如需保存到文件，可使用重定向：

```bash
# 输出到 TEMP\testlog 目录
"InvoiceAI.App.exe" --test --all > TEMP\testlog\test_results.txt 2>&1
type TEMP\testlog\test_results.txt
```

**测试过程中产生的文件**:

- 测试日志: `TEMP\testlog\` 目录（手动重定向）
- 导出测试文件: `TEMP\testlog\test_export_*.xlsx`（自动清理）

---

## 3. 参数说明

### 3.1 `--test` / `-t`

进入测试模式。没有此参数时正常启动 MAUI 窗口。

```bash
# 进入测试模式
InvoiceAI.App.exe --test

# 简写
InvoiceAI.App.exe -t
```

### 3.2 `--case=<功能名称>`

指定运行单个测试用例。

| 可用值         | 测试功能   | 耗时        | 依赖外部服务       |
| ----------- | ------ | --------- | ------------ |
| `load`      | 数据加载   | 快 (~1s)   | 否            |
| `category`  | 分类管理   | 快 (~1s)   | 否            |
| `search`    | 发票搜索   | 快 (~1s)   | 否            |
| `delete`    | 发票删除   | 快 (~1s)   | 否            |
| `edit`      | 编辑保存   | 快 (~1s)   | 否            |
| `saved`     | 已保存列表  | 快 (~1s)   | 否            |
| `imagepath` | 图片路径查找 | 快 (~1s)   | 否            |
| `export`    | 发票导出   | 中 (~5s)   | 否            |
| `import`    | 发票导入   | 慢 (~30s+) | 是 (OCR + AI) |

### 3.3 `--all`

运行全部 8 个测试用例。

```bash
# 运行全部
InvoiceAI.App.exe --test --all

# 不带 --case 时也运行全部
InvoiceAI.App.exe --test
```

### 3.4 无效参数处理

```bash
# 无效的 case 值
InvoiceAI.App.exe --test --case=invalid
```

输出:

```
=== 测试 [invalid] 开始 ===
输入/条件: N/A
预期结果: 有效的测试用例
实际结果: N/A
截图路径: N/A
状态: FAIL: 未知的测试用例: invalid。可用: load, category, search, import, export, delete, saved, edit
=== 测试结束 ===
```

---

## 4. 测试用例详解

### 4.1 `load` — 数据加载测试

**测试目标**: 验证 `GetUnconfirmedAsync()` 正确返回未确认发票。

**执行步骤**:

1. 确保数据库初始化（`EnsureCreatedAsync`）
2. 查询所有未确认发票（`IsConfirmed=false`）
3. 验证返回列表中所有发票的 `IsConfirmed` 均为 `false`

**成功条件**:

- 返回列表中每张发票的 `IsConfirmed == false`

**典型输出**:

```
=== 测试 [load] 开始 ===
输入/条件: 查询未确认发票列表 (数据库共有 15 条记录)
预期结果: 返回所有 IsConfirmed=false 的发票列表
实际结果: 获取 8 条未确认发票记录，全部 IsConfirmed=false: True
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

### 4.2 `category` — 分类管理测试

**测试目标**: 验证 `GetCategoryCountsAsync()` 和 `GetDistinctCategoriesAsync()` 正常工作。

**执行步骤**:

1. 查询各分类的发票数量
2. 查询所有不同分类列表
3. 验证返回非空字典

**成功条件**:

- `GetCategoryCountsAsync()` 返回非 null 字典

**典型输出**:

```
=== 测试 [category] 开始 ===
输入/条件: 获取分类计数 + 按分类筛选 (共 3 个分类: 交通費, 電気・ガス, 食料品)
预期结果: 返回非空分类字典，按分类筛选返回对应发票
实际结果: 分类计数: 交通費:5, 電気・ガス:3, 食料品:7
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

### 4.3 `search` — 发票搜索测试

**测试目标**: 验证发票搜索功能（按发行方名、描述、登録番号模糊匹配）。

**执行步骤**:

1. 获取数据库中第一条发票的 `IssuerName` 作为搜索词
2. 执行模糊匹配搜索（与 MainPage 的搜索逻辑一致）
3. 验证搜索结果包含目标发票

**成功条件**:

- 搜索结果非空且至少有一条 `IssuerName == searchTerm`

**特殊情况**:

- 数据库为空 → **SKIP**（不视为失败）

**典型输出**:

```
=== 测试 [search] 开始 ===
输入/条件: 搜索词: "東京電力" (数据库共 15 条)
预期结果: 返回包含 "東京電力" 的发票
实际结果: 找到 3 条匹配记录
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

### 4.4 `import` — 发票导入测试

**测试目标**: 验证完整的 OCR → AI → 保存流程。

**执行步骤**:

1. 在 `TEMP\testdata\` 目录下查找 jpg/png/pdf 测试文件
2. 调用 `BaiduOcrService.RecognizeAsync()` 进行 OCR 识别
3. 调用 `GlmService.ProcessBatchAsync()` 进行 AI 分析
4. 将解析结果保存到数据库
5. 验证数据库记录数 +1
6. **清理**: 删除测试导入的发票

**成功条件**:

- 导入后数据库记录数 = 导入前 + 1
- 清理后恢复到导入前数量

**SKIP 条件** (不视为失败):

- 找不到测试图片 → `无测试图片，跳过导入测试`（需在 `TEMP\testdata\` 放置测试文件）
- OCR 返回空 → `OCR 服务不可用，跳过导入测试`
- AI 返回空 → `AI 服务不可用，跳过导入测试`
- 任何异常 → `导入流程异常: <error>`

**典型输出**:

```
=== 测试 [import] 开始 ===
输入/条件: 导入: 185.jpg
预期结果: OCR → AI → 保存，数据库记录数 +1 (导入前: 15, 导入后: 16)
实际结果: 成功导入并清理，数据库恢复到 15 条
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

### 4.5 `export` — 发票导出测试

**测试目标**: 验证 Excel 导出功能生成有效文件。

**执行步骤**:

1. 查询所有发票
2. 调用 `ExcelExportService.ExportAsync()` 导出到临时 `.xlsx` 文件
3. 验证文件存在且大小 > 0
4. **清理**: 删除导出的临时文件

**成功条件**:

- 生成的 `.xlsx` 文件存在且 `Length > 0`

**典型输出**:

```
=== 测试 [export] 开始 ===
输入/条件: 导出 15 条发票到 Excel
预期结果: 生成有效的 .xlsx 文件
实际结果: 导出成功，文件大小: 4521 字节 (已清理)
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

### 4.6 `delete` — 发票删除测试

**测试目标**: 验证创建和删除发票的 CRUD 完整性。

**执行步骤**:

1. 记录当前数据库记录数（`beforeCount`）
2. 创建专用测试发票（`IssuerName="测试删除专用"`）
3. 验证记录数 +1（`afterSaveCount == beforeCount + 1`）
4. 删除该测试发票
5. 验证记录数恢复（`afterDeleteCount == beforeCount`）
6. 验证 `GetByIdAsync(已删除ID)` 返回 `null`

**成功条件**:

- 三个条件同时满足:
  1. `afterSaveCount == beforeCount + 1`
  2. `afterDeleteCount == beforeCount`
  3. `GetByIdAsync(已删除ID) == null`

**典型输出**:

```
=== 测试 [delete] 开始 ===
输入/条件: 创建测试发票 → 删除 (导入前: 15, 创建后: 16, 删除后: 15)
预期结果: 删除后 GetByIdAsync 返回 null，记录数恢复到删除前
实际结果: 删除成功，数据库记录数正确恢复
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

### 4.7 `imagepath` — 发票图片路径查找测试

**测试目标**: 验证 `FindInvoiceImagePath` 能正确找到发票图片文件。

**执行步骤**:

1. 查询数据库中所有发票
2. 查找第一张有 `SourceFilePath` 的发票
3. 验证 `SourceFilePath` 指向的文件是否存在
4. 如不存在，检查 `TEMP\testdata\` 目录是否有图片可作为 fallback
5. 返回验证结果

**成功条件**:

- `SourceFilePath` 指向的文件存在，或
- `TEMP\testdata\` 目录有图片可作为 fallback

**典型输出**:

```
=== 测试 [imagepath] 开始 ===
输入/条件: 查找发票图片路径 (发行方: コスモ石油販売（株）西関東カンパニー)
预期结果: SourceFilePath 指向的文件存在，或 TEMP\testdata 有图片可用作 fallback
实际结果: 发票 SourceFilePath: C:\...\TEMP\testdata\IMG_20260405_104815.jpg
  文件存在: True
  testdata 目录图片数: 6
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

### 4.8 `saved` — 已保存列表测试

**测试目标**: 验证已确认发票查询和时间范围查询。

**执行步骤**:

1. 查询所有已确认发票（`IsConfirmed=true`）
2. 验证返回的所有发票 `IsConfirmed == true`
3. 如果有已确认发票，按 `CreatedAt` 时间范围查询
4. 验证时间范围查询返回非空结果

**成功条件**:

- 所有已确认发票的 `IsConfirmed == true`
- 时间范围查询成功（当有已确认发票时）

**典型输出**:

```
=== 测试 [saved] 开始 ===
输入/条件: 查询已确认发票列表 (共 15 条，已确认: 7)
预期结果: 返回所有 IsConfirmed=true 的发票 (7 条)
实际结果: 已确认发票: 7 条, 全部 IsConfirmed=true: True, 时间范围查询: 7 条
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

### 4.9 `edit` — 编辑保存测试

**测试目标**: 验证发票字段的更新能力。

**执行步骤**:

1. 创建专用测试发票（`Description="编辑前内容"`, `Category="测试"`, `TaxIncludedAmount=200`）
2. 修改字段: `Description="编辑后内容"`, `Category="测试_已编辑"`, `TaxIncludedAmount=300`
3. 调用 `UpdateAsync()` 保存
4. 重新读取记录（`GetByIdAsync`）
5. 验证三个字段均正确更新
6. **清理**: 删除测试发票

**成功条件**:

- `reloaded.Description == "编辑后内容"`
- `reloaded.Category == "测试_已编辑"`
- `reloaded.TaxIncludedAmount == 300`

**典型输出**:

```
=== 测试 [edit] 开始 ===
输入/条件: 创建发票 → 编辑 Description/Category/Amount → 保存 → 重新读取
预期结果: UpdateAsync 后数据库记录正确更新
实际结果: 编辑成功: Description="编辑后内容", Category="测试_已编辑", Amount=300 (已清理)
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

---

## 5. 输出格式说明

### 5.1 标准输出格式

每个测试用例遵循以下格式:

```
=== 测试 [<caseName>] 开始 ===
输入/条件: <测试输入或前置条件>
预期结果: <期望的行为或结果>
实际结果: <实际观察到的行为或结果>
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

### 5.2 状态说明

| 状态                 | 含义           | 退出码贡献 |
| ------------------ | ------------ | ----- |
| **PASS**           | 测试通过，功能正常    | +0    |
| **FAIL: <原因>**     | 测试失败，功能异常    | +1    |
| **SKIP** (视为 PASS) | 测试跳过，外部条件不满足 | +0    |

### 5.3 汇总输出

运行 `--all` 时，在所有单个测试输出后追加汇总:

```
╔══════════════════════════════════════════╗
║              测试汇总                      ║
╚══════════════════════════════════════════╝
通过: 7/8
失败: 1
```

### 5.4 退出码

| 退出码 | 含义                       |
| --- | ------------------------ |
| `0` | 所有测试通过（或 SKIP）           |
| `1` | 至少一个测试失败，或无效的 `--case` 值 |
| `2` | 测试模式启动失败（如 DI 构建失败）      |

---

## 6. 常见场景示例

### 场景 1: 快速验证核心功能

```bash
# 只运行不依赖外部服务的快速测试
InvoiceAI.App.exe --test --case=load
InvoiceAI.App.exe --test --case=delete
InvoiceAI.App.exe --test --case=edit
```

### 场景 2: 导入功能诊断

```bash
# 诊断导入功能是否正常工作
InvoiceAI.App.exe --test --case=import > import_test.txt 2>&1
type import_test.txt
```

如果输出显示 `OCR 服务不可用` 或 `AI 服务不可用`，说明对应的 API 配置有问题。

### 场景 3: 数据库完整性检查

```bash
# 运行所有数据库操作相关测试
InvoiceAI.App.exe --test --case=load
InvoiceAI.App.exe --test --case=category
InvoiceAI.App.exe --test --case=search
InvoiceAI.App.exe --test --case=saved
InvoiceAI.App.exe --test --case=delete
InvoiceAI.App.exe --test --case=edit
```

### 场景 4: 回归测试（完整）

```bash
# 每次代码修改后运行完整测试
InvoiceAI.App.exe --test --all > regression_test.txt 2>&1
type regression_test.txt
```

### 场景 5: CI/CD 集成示例

```bash
# 在 CI 脚本中使用
InvoiceAI.App.exe --test --all
exit_code=$?
if [ $exit_code -ne 0 ]; then
    echo "测试失败！"
    exit 1
fi
echo "所有测试通过"
```

### 场景 6: 仅运行快速测试（排除 import）

```bash
# import 测试依赖 OCR/AI 服务，耗时较长。其他测试均为秒级。
InvoiceAI.App.exe --test --case=load
InvoiceAI.App.exe --test --case=category
InvoiceAI.App.exe --test --case=search
InvoiceAI.App.exe --test --case=export
InvoiceAI.App.exe --test --case=delete
InvoiceAI.App.exe --test --case=saved
InvoiceAI.App.exe --test --case=edit
```

---

## 7. 测试结果解读

### 7.1 PASS — 测试通过

```
状态: PASS
```

**含义**: 功能正常，无需干预。

---

### 7.2 FAIL — 测试失败

```
状态: FAIL: 存在 IsConfirmed=true 的发票在返回列表中
```

**含义**: 功能异常，需要排查。

**常见原因**:
| 错误信息 | 原因 | 解决方案 |
|----------|------|----------|
| `SQLite Error 14: unable to open database file` | 数据库路径不存在或无权限 | 确保 `%APPDATA%/InvoiceAI/` 目录存在 |
| `存在 IsConfirmed=true 的发票在返回列表中` | `GetUnconfirmedAsync()` 查询逻辑有误 | 检查 EF Core 查询条件 |
| `未找到包含搜索词 "XXX" 的发票` | 搜索算法或数据问题 | 检查搜索词是否正确、数据是否存在 |
| `导入后记录数不匹配` | 导入或清理逻辑有误 | 检查 SaveAsync 和 DeleteAsync |

---

### 7.3 SKIP — 测试跳过

```
状态: PASS
(实际为 SKIP，但在汇总中计为通过)
```

**含义**: 外部条件不满足，无法执行测试，但不视为失败。

**常见 SKIP 原因**:
| 消息 | 含义 |
|------|------|
| `数据库为空，跳过` | 数据库中没有发票记录，搜索测试自动跳过 |
| `无测试图片，跳过导入测试` | `invoices/` 目录下没有 jpg/png 图片 |
| `OCR 服务不可用，跳过导入测试` | PaddleOCR API 未配置或不可达 |
| `AI 服务不可用，跳过导入测试` | GLM API 未配置或不可达 |
| `导入流程异常` | OCR→AI→保存 流程中抛出异常 |

---

## 8. 故障排查

### 8.1 无输出或空输出

**现象**: 运行命令后没有任何输出。

**原因**: MAUI Windows 应用在 GUI 模式下运行时，控制台输出可能被重定向或不可见。

**解决方案**:

```bash
# 重定向到文件后查看
InvoiceAI.App.exe --test --case=load > test.txt 2>&1
type test.txt
```

### 8.2 退出码始终为 1

**排查步骤**:

```bash
# 查看完整输出
InvoiceAI.App.exe --test --all > full_test.txt 2>&1
type full_test.txt
```

查看哪个测试用例显示 `FAIL`，根据错误信息排查。

### 8.3 数据库连接失败

**错误**: `SQLite Error 14: unable to open database file`

**原因**: 测试模式下数据库路径可能不正确。

**解决方案**:

1. 确认数据库文件存在: `%APPDATA%\InvoiceAI\invoices.db`

2. 检查目录权限

3. 查看 `TestRunner.BuildTestServices()` 中的路径构建逻辑:
   
   ```csharp
   var dbPath = Path.Combine(
       Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
       "InvoiceAI", "invoices.db");
   ```

### 8.4 import 测试超时

**原因**: OCR 和 AI 服务调用可能需要较长时间（尤其网络不佳时）。

**解决方案**:

- 确认 OCR 和 AI 服务配置正确（`appsettings.json`）

- 单独运行 import 测试，设置更长超时:
  
  ```bash
  InvoiceAI.App.exe --test --case=import > import.txt 2>&1
  ```

---

## 9. 如何添加新测试用例

### 9.1 步骤概览

1. 在 `TestCases.cs` 中添加测试方法
2. 在 `TestRunner.RunSingleAsync()` 中添加路由
3. 更新测试列表数组（`RunAllAsync` 中的 `cases` 数组）
4. 编译验证

### 9.2 详细步骤

#### 步骤 1: 添加测试方法

在 `src/InvoiceAI.App/TestCases.cs` 中添加:

```csharp
public static async Task<TestCaseResult> TestNewFeature(IServiceProvider services)
{
    // 1. 获取所需服务
    var invoiceService = services.GetRequiredService<IInvoiceService>();

    // 2. 确保数据库初始化
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    // 3. 执行测试逻辑
    var result = await invoiceService.SomeMethodAsync();

    // 4. 验证结果
    bool passed = result != null && result.Count > 0;

    // 5. 返回结果
    return new TestCaseResult(
        "newfeature",                       // case 名称（小写，用于 --case 参数）
        "测试描述",                          // 输入/条件
        "期望的结果",                        // 预期结果
        $"实际观察: {result?.Count ?? 0}",  // 实际结果
        null,                               // 截图路径 (N/A)
        passed,                             // 是否通过
        passed ? null : "失败原因说明"      // 失败时的原因
    );
}
```

#### 步骤 2: 添加路由

在 `src/InvoiceAI.App/TestRunner.cs` 的 `RunSingleAsync` 方法中添加:

```csharp
var result = caseName.ToLower() switch
{
    // ... 现有的 case ...
    "newfeature" => await TestCases.TestNewFeature(services),
    _ => new TestCaseResult(...)
};
```

#### 步骤 3: 更新测试列表

在 `RunAllAsync` 方法中更新 `cases` 数组:

```csharp
var cases = new[] { "load", "category", "search", "import", "export", "delete", "saved", "edit", "newfeature" };
```

#### 步骤 4: 编译验证

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
```

### 9.3 测试方法命名约定

| 方法名              | case 值       | 说明    |
| ---------------- | ------------ | ----- |
| `TestLoad`       | `load`       | 数据加载  |
| `TestCategory`   | `category`   | 分类管理  |
| `TestSearch`     | `search`     | 搜索    |
| `TestImport`     | `import`     | 导入    |
| `TestExport`     | `export`     | 导出    |
| `TestDelete`     | `delete`     | 删除    |
| `TestSaved`      | `saved`      | 已保存列表 |
| `TestEdit`       | `edit`       | 编辑保存  |
| `TestNewFeature` | `newfeature` | 新功能   |

---

## 10. 架构原理

### 10.1 启动流程

```
InvoiceAI.App.exe --test --case=load
    │
    ▼
Windows App.xaml.cs (MauiWinUIApplication)
    │ OnLaunched()
    │ Environment.GetCommandLineArgs()
    │ 检测到 --test
    ▼
跳过 base.OnLaunched() ── 不创建 MAUI 窗口
    │
    ▼
RunTestModeAsync(args)
    │
    ▼
TestRunner.RunAsync(args)
    │
    ├── 解析参数 (--test, --case=X, --all)
    │
    ├── BuildTestServices()
    │   └── ServiceCollection 注册核心服务
    │
    ├── RunSingleAsync("load", services)
    │   └── TestCases.TestLoad(services)
    │
    ├── PrintResult(result)
    │
    └── Environment.Exit(exitCode)
```

### 10.2 无头模式 vs 正常模式

| 对比项       | 正常模式                          | 测试模式                             |
| --------- | ----------------------------- | -------------------------------- |
| **窗口创建**  | `base.OnLaunched()` 创建窗口      | 跳过，不创建                           |
| **DI 构建** | `MauiProgram.CreateMauiApp()` | `TestRunner.BuildTestServices()` |
| **服务注册**  | 全部服务 (含 ViewModels/Pages)     | 仅核心业务服务                          |
| **退出方式**  | 用户手动关闭                        | `Environment.Exit(exitCode)`     |
| **输出方式**  | UI 界面                         | 控制台 (stdout)                     |

### 10.3 测试模式 DI 容器

```csharp
// 注册的服务
AppDbContext          ← SQLite 数据库
IAppSettingsService   ← 配置管理
IFileService          ← 文件处理
IBaiduOcrService      ← OCR 识别
IGlmService           ← AI 分析
IInvoiceService       ← 发票 CRUD
IExcelExportService   ← Excel 导出
HttpClient            ← 网络请求

// 不注册的服务 (测试模式不需要)
MainViewModel
InvoiceDetailViewModel
ImportViewModel
SettingsViewModel
SavedInvoicesViewModel
MainPage
SettingsPage
SavedInvoicesWindow
字体/资源注册
```

---

## 11. 最佳实践

### 11.1 日常开发

```bash
# 每次修改代码后快速验证
InvoiceAI.App.exe --test --case=load && InvoiceAI.App.exe --test --case=delete

# 完整回归测试
InvoiceAI.App.exe --test --all
```

### 11.2 排查问题

```bash
# 将输出保存到文件方便分析
InvoiceAI.App.exe --test --all > debug_output.txt 2>&1

# 针对单个功能诊断
InvoiceAI.App.exe --test --case=import > import_debug.txt 2>&1
```

### 11.3 注意事项

1. **直接在主数据库上运行**: 测试会读写 `invoices.db` 主数据库。破坏性测试（delete/edit）会自动清理，但建议在运行前备份数据库。

2. **import 测试依赖外部服务**: 需要 PaddleOCR 和 GLM API 配置正确才能通过。如果服务不可用，会标记为 SKIP（不视为失败）。

3. **测试数据准备**: 将测试用发票图片（JPG/PNG/PDF）放在 `TEMP\testdata\` 目录下。import 测试会自动查找该目录下的测试文件。

4. **测试日志输出**: 测试过程中的临时文件（如导出的 Excel）保存在 `TEMP\testlog\` 目录，导出测试文件会自动清理。

5. **并发运行**: 不建议同时运行多个测试实例，可能导致数据库锁竞争。

### 11.4 推荐的测试频率

| 场景       | 推荐测试                      |
| -------- | ------------------------- |
| 修改 UI 代码 | 运行快速测试 (load/delete/edit) |
| 修改业务逻辑   | 运行 `--all`                |
| 修改服务层接口  | 运行 `--all` + 检查 import    |
| 发布前      | 运行 `--all`，确认全部通过         |

---

## 附录: 文件位置

| 文件/目录      | 路径                                                                                   |
| ---------- | ------------------------------------------------------------------------------------ |
| 测试调度器      | `src/InvoiceAI.App/TestRunner.cs`                                                    |
| 测试用例       | `src/InvoiceAI.App/TestCases.cs`                                                     |
| Windows 入口 | `src/InvoiceAI.App/Platforms/Windows/App.xaml.cs`                                    |
| 数据库        | `%APPDATA%\InvoiceAI\invoices.db`                                                    |
| 测试数据目录     | `TEMP\testdata\`（放置测试用发票图片）                                                          |
| 测试日志目录     | `TEMP\testlog\`（测试输出和临时文件）                                                           |
| 可执行文件      | `src/InvoiceAI.App/bin/Debug/net9.0-windows10.0.19041.0/win10-x64/InvoiceAI.App.exe` |

### 测试数据说明

`TEMP\testdata\` 目录用于存放测试用发票文件，支持以下格式:

- 图片: `.jpg`, `.jpeg`, `.png`
- PDF: `.pdf`

import 测试会自动扫描该目录并选取第一个可用文件作为测试输入。

示例文件:

```
TEMP\testdata\
├── 185.jpg                    # 单张发票图片
├── 2024.10交通.pdf            # PDF 发票
├── IMG_20260405_104703.jpg    # 拍照发票
└── ...
```
