# Invoice AI Assistant

日本发票智能识别与管理工具 - 基于 .NET MAUI 9 的 Windows 桌面应用，支持日本适格请求书（適格請求書）的自动化识别、解析和管理。

## 功能特性

### 核心功能
- 📄 **多格式支持** - 支持 JPG、PNG、PDF 格式的发票文件
- 🔍 **OCR 识别** - 基于 PaddleOCR-VL-1.5 的文字识别
- 🤖 **AI 解析** - 使用 GLM-4.7 大模型进行智能发票信息提取
- 🔄 **提供商故障转移** - 支持多个 GLM 提供商之间的自动故障转移
- 💾 **本地数据库** - SQLite 存储，支持发票数据的增删改查
- 📊 **Excel 导出** - 使用 MiniExcel 导出发票数据

### 故障转移功能
- ⚙️ **多提供商支持** - 智谱 (Zhipu)、NVIDIA NIM、Cerebras
- ✅ **连接测试** - 设置页面测试各提供商连接状态
- 🔄 **自动切换** - 主提供商失败时自动切换到备用提供商
- 🔙 **自动恢复** - 导入完成后恢复原始提供商设置

## 技术栈

### 框架与语言
- **.NET 9** + **C#**
- **.NET MAUI 9** - Windows 桌面应用
- **Windows 10/11** (目标平台: `net9.0-windows10.0.19041.0`)

### 核心库
- **CommunityToolkit.Mvvm** - MVVM 框架
- **CommunityToolkit.Maui.Markup** - C# 标记式 UI
- **EF Core** + **SQLite** - 数据库 ORM
- **MiniExcel** - Excel 导出（无许可证问题）

### AI 服务
- **PaddleOCR-VL-1.5** - OCR 文字识别
- **GLM-4.7** - 大语言模型（多提供商支持）

## 项目结构

```
InvoiceAI/
├── src/
│   ├── InvoiceAI.Models/          # POCO 实体模型
│   ├── InvoiceAI.Data/            # EF Core + SQLite
│   ├── InvoiceAI.Core/            # 业务逻辑（Services、ViewModels、Prompts）
│   └── InvoiceAI.App/             # MAUI 应用入口
├── tests/
│   └── InvoiceAI.Core.Tests/      # 单元测试（63 个测试全部通过）
├── docs/                           # 项目文档（本地保留，未提交）
├── appsettings.json               # 配置文件（未提交）
└── InvoiceAI.sln                  # 解决方案文件
```

## 安装与运行

### 前置要求
- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022 或更高版本（可选）

### 构建项目

```bash
# 克隆仓库
git clone https://github.com/tothemoonai/InvoiceAI.git
cd InvoiceAI

# 还原依赖
dotnet restore

# 构建完整解决方案
dotnet build InvoiceAI.sln

# 或仅构建 Windows 应用（更快）
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj -f net9.0-windows10.0.19041.0
```

### 运行应用

```bash
# 运行 Windows 桌面应用
dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -f net9.0-windows10.0.19041.0
```

### 运行测试

```bash
# 运行所有测试
dotnet test tests/InvoiceAI.Core.Tests/

# 运行单个测试
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~TestMethodName"
```

## 配置说明

首次运行时，需要在应用 exe 同目录下创建 `appsettings.json` 文件：

```json
{
  "BaiduOcr": {
    "Token": "your-paddleocr-token",
    "Endpoint": "https://aip.baidubce.com/rest/2.0/ocr/v1/general/basic"
  },
  "Glm": {
    "Provider": "zhipu",
    "ApiKey": "your-api-key",
    "Endpoint": "https://open.bigmodel.cn/api/paas/v4/chat/completions",
    "Model": "glm-4.7",
    "VerifiedProviders": []
  }
}
```

### 配置项说明

| 配置项 | 说明 |
|--------|------|
| `BaiduOcr.Token` | PaddleOCR API Token |
| `BaiduOcr.Endpoint` | PaddleOCR API 端点地址 |
| `Glm.Provider` | 当前使用的提供商（zhipu/nvidia/cerebras）|
| `Glm.ApiKey` | 智谱 API Key |
| `Glm.NvidiaApiKey` | NVIDIA NIM API Key |
| `Glm.CerebrasApiKey` | Cerebras API Key |
| `Glm.VerifiedProviders` | 用户测试通过的提供商列表 |

## 使用指南

### 1. 配置提供商
1. 打开应用，进入设置页面
2. 配置 PaddleOCR Token 和端点
3. 配置 GLM 提供商（可选择智谱、NVIDIA NIM 或 Cerebras）
4. 点击"测试连接"验证配置

### 2. 导入发票
1. 拖拽 JPG/PNG/PDF 文件到应用界面
2. 应用自动执行：
   - OCR 文字识别
   - AI 智能解析发票信息
   - 保存到本地数据库
   - 归档到指定文件夹

### 3. 管理发票
- **查看** - 主页面三栏布局显示所有发票
- **编辑** - 点击发票详情进行编辑
- **导出** - 导出为 Excel 文件

## 故障转移机制

当主提供商失败时，系统会自动切换到用户已测试通过的备用提供商：

1. **设置页面测试连接** → 系统记录为"已验证"
2. **导入时主提供商失败** → 自动切换到下一个已验证的提供商
3. **导入完成后** → 自动恢复原始提供商设置

## 开发指南

### 数据库迁移
```bash
# 添加新迁移
dotnet ef migrations add MigrationName --startup-project src/InvoiceAI.App/InvoiceAI.App.csproj

# 更新数据库
dotnet ef database update --startup-project src/InvoiceAI.App/InvoiceAI.App.csproj
```

### 调试文件位置
- OCR 识别结果: `%TEMP%/InvoiceAI/ocr/*.md`
- 错误日志: `%TEMP%/InvoiceAI/import_error.log`
- 故障转移日志: `{项目根}/TEMP/errorlog/provider_fallback.log`

## 最近更新

- ✅ **多提供商故障转移** - 支持智谱、NVIDIA NIM、Cerebras 之间的自动切换
- ✅ **连接测试** - 设置页面测试各提供商连接状态并标记为已验证
- ✅ **提供商恢复** - 导入完成后自动恢复原始提供商设置
- ✅ **状态通知** - 导入过程中实时显示提供商切换状态

## 许可证

本项目为私有仓库，保留所有权利。

## 联系方式

- GitHub: [@tothemoonai](https://github.com/tothemoonai)
