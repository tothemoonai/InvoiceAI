namespace InvoiceAI.App.Utils;

/// <summary>
/// 提供主题感知的颜色访问。所有颜色从 XAML 资源字典读取，
/// 自动根据 Light/Dark 主题返回对应值。
/// </summary>
public static class ThemeManager
{
    /// <summary>当前是否为暗色主题</summary>
    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    /// <summary>获取 Light/Dark 颜色</summary>
    public static Color Get(string lightKey, string darkKey)
    {
        var key = IsDark ? darkKey : lightKey;
        return GetColor(key);
    }

    /// <summary>获取单一颜色键 (自动主题)</summary>
    public static Color Get(string key) => GetColor(key);

    // ─── 便捷属性 ─────────────────────────────────────────

    public static Color Background => Get("BackgroundPrimary", "DarkBackgroundPrimary");
    public static Color CardBackground => Get("BackgroundSecondary", "DarkBackgroundSecondary");
    public static Color TextPrimary => Get("TextPrimary", "DarkTextPrimary");
    public static Color TextSecondary => Get("TextSecondary", "DarkTextSecondary");
    public static Color TextTertiary => Get("TextTertiary", "DarkTextTertiary");
    public static Color BorderLight => Get("BorderLight", "DarkBorder");
    public static Color BorderMedium => Get("BorderMedium", "DarkBorder");
    public static Color BrandPrimary => Get("BrandPrimary", "BrandPrimary");
    public static Color BrandAccent => Get("BrandAccent", "BrandAccent");
    public static Color Success => Get("Success", "Success");
    public static Color Error => Get("Error", "Error");
    public static Color Warning => Get("Warning", "Warning");
    public static Color Info => Get("Info", "Info");
    public static Color SelectedBackground => Get("SelectedBackground", "DarkSelectedBackground");
    public static Color HoverBackground => Get("HoverBackground", "DarkHoverBackground");

    // ─── 内部实现 ─────────────────────────────────────────

    private static Color GetColor(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        // 回退值 (防止资源未加载时崩溃)
        return key switch
        {
            "BackgroundPrimary" or "DarkBackgroundPrimary" => Color.FromArgb("#F8F9FA"),
            "BackgroundSecondary" or "DarkBackgroundSecondary" => Colors.White,
            "TextPrimary" or "DarkTextPrimary" => Color.FromArgb("#2D3436"),
            "TextSecondary" or "DarkTextSecondary" => Color.FromArgb("#636E72"),
            "TextTertiary" or "DarkTextTertiary" => Color.FromArgb("#B2BEC3"),
            "BorderLight" or "DarkBorder" => Color.FromArgb("#E0E6ED"),
            "BrandPrimary" => Color.FromArgb("#6C5CE7"),
            "BrandAccent" => Color.FromArgb("#00CEC9"),
            "Success" => Color.FromArgb("#00B894"),
            "Error" => Color.FromArgb("#FF6B6B"),
            "Warning" => Color.FromArgb("#FECA57"),
            "Info" => Color.FromArgb("#54A0FF"),
            "SelectedBackground" or "DarkSelectedBackground" => Color.FromArgb("#E8EAF6"),
            _ => Colors.Transparent
        };
    }
}
