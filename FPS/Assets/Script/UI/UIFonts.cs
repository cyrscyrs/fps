using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 字体小工具：uGUI 自带的字体没有中文字形，直接用会显示成方框。
/// 这里在运行时创建一个「系统字体」的动态字体，把界面上的 Text 都换成它（Windows 下有微软雅黑 / 黑体）。
/// </summary>
public static class UIFonts
{
    private static Font cachedFont;

    // 按顺序尝试，第一个能用的就用
    private static readonly string[] FontNames =
    {
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "SimHei",
        "SimSun",
        "Noto Sans CJK SC",
        "PingFang SC",
        "Arial"
    };

    public static Font Get()
    {
        if (cachedFont != null) return cachedFont;

        cachedFont = Font.CreateDynamicFontFromOSFont(FontNames, 36);

        if (cachedFont == null)   // 实在没有系统字体就用 Unity 内置的
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return cachedFont;
    }

    /// <summary>把某个物体（含子物体）上所有 Text 的字体换成支持中文的动态字体。</summary>
    public static void Apply(GameObject root)
    {
        if (root == null) return;

        Font font = Get();
        if (font == null) return;

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            text.font = font;
        }
    }
}
