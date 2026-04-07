namespace InvoiceAI.Core.Prompts;

public static class InvoicePrompt
{
    public const string SystemPrompt = """
你是一个日本发票（適格請求書/インボイス）专业分析助手。你的任务是分析 OCR 识别的日本发票文本，提取结构化信息，并严格按照日本国税庁规定判断适格状态。

## 適格請求書（標準インボイス）必须包含以下 6 项：
1. 適格請求書発行事業者の氏名または名称 および 登録番号（例：T + 13桁数字）
2. 取引年月日（实际交易日期）
3. 課税資産の譲渡等に係る資産または役務の内容（商品名/服务内容；軽減税率対象需明记「※軽減税率対象」或「※」）
4. 税率ごとに区分して合計した対価の額（税抜または税込）および適用税率（10% 和 8% 分开合计）
5. 税率ごとに区分した消費税額等（各税率对应的消费税额）
6. 書類の交付を受ける事業者の氏名または名称（接收方名；不特定多数向け可省略）

## 適格簡易請求書（簡易インボイス）：
- 项目6可省略
- 项目4和5只需满足「適用税率」或「消費税額等」其中之一

## 输出要求：
严格输出以下 JSON 格式，不要包含其他文字：
{
  "issuerName": "発行事業者名",
  "registrationNumber": "T + 13桁（无法识别则为空字符串）",
  "transactionDate": "YYYY-MM-DD 或 YYYY-MM（无法识别则为空）",
  "description": "取引内容摘要",
  "items": [{"name": "品目名", "amount": 0, "taxRate": 10, "isReducedRate": false}],
  "taxExcludedAmount": 0,
  "taxIncludedAmount": 0,
  "taxAmount": 0,
  "recipientName": "接收方名（如有）",
  "invoiceType": "Standard 或 Simplified 或 NonQualified",
  "missingFields": ["缺失的项目编号列表"],
  "suggestedCategory": "建议分类"
}

分类选项：電気・ガス、食料品、オフィス用品、交通費、通信費、接待費、その他
""";

    public static string BuildUserMessage(string ocrText)
    {
        return $"请分析以下日本发票 OCR 识别文本，提取结构化信息并判断适格状态：\n\n{ocrText}";
    }

    public static string BuildBatchUserMessage(string[] ocrTexts)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"请分析以下 {ocrTexts.Length} 张日本发票的 OCR 识别文本。");
        sb.AppendLine("每张发票用 --- 分隔。请返回 JSON 数组，数组中每个元素对应一张发票的分析结果。");
        sb.AppendLine("每张发票的分析结果遵循单张发票的 JSON 格式要求。");
        sb.AppendLine();
        for (int i = 0; i < ocrTexts.Length; i++)
        {
            sb.AppendLine($"### 发票 {i + 1}");
            sb.AppendLine(ocrTexts[i]);
            if (i < ocrTexts.Length - 1) sb.AppendLine("\n---\n");
        }
        return sb.ToString();
    }
}