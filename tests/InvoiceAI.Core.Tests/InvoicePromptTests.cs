using InvoiceAI.Core.Prompts;

namespace InvoiceAI.Core.Tests;

public class InvoicePromptTests
{
    [Fact]
    public void SystemPrompt_ContainsRequiredSections()
    {
        Assert.Contains("適格請求書", InvoicePrompt.SystemPrompt);
        Assert.Contains("登録番号", InvoicePrompt.SystemPrompt);
        Assert.Contains("取引年月日", InvoicePrompt.SystemPrompt);
        Assert.Contains("消費税額", InvoicePrompt.SystemPrompt);
    }

    [Fact]
    public void SystemPrompt_ContainsJsonFormat()
    {
        Assert.Contains("issuerName", InvoicePrompt.SystemPrompt);
        Assert.Contains("registrationNumber", InvoicePrompt.SystemPrompt);
        Assert.Contains("invoiceType", InvoicePrompt.SystemPrompt);
        Assert.Contains("missingFields", InvoicePrompt.SystemPrompt);
        Assert.Contains("suggestedCategory", InvoicePrompt.SystemPrompt);
    }

    [Fact]
    public void SystemPrompt_ContainsCategoryOptions()
    {
        Assert.Contains("電気・ガス", InvoicePrompt.SystemPrompt);
        Assert.Contains("食料品", InvoicePrompt.SystemPrompt);
        Assert.Contains("交通費", InvoicePrompt.SystemPrompt);
        Assert.Contains("その他", InvoicePrompt.SystemPrompt);
    }

    [Fact]
    public void BuildUserMessage_WrapsOcrText()
    {
        var ocrText = "テスト電気 2025年3月 ¥5,500";
        var result = InvoicePrompt.BuildUserMessage(ocrText);
        Assert.Contains(ocrText, result);
        Assert.Contains("分析", result);
    }

    [Fact]
    public void BuildUserMessage_EmptyText_StillWraps()
    {
        var result = InvoicePrompt.BuildUserMessage("");
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
