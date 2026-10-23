using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;

namespace BoolDownload.Services;

/// <summary>
/// 试验性毛玻璃效果：为窗口申请 Windows 11 云母（Mica）背板，并把窗口底色换成半透明层，
/// 使背板透出。关闭时移除窗口级资源覆盖，恢复主题里的不透明底色。
/// </summary>
public static class MicaEffect
{
    private static readonly List<Window> Windows = new();

    private static readonly WindowTransparencyLevel[] MicaHint = { WindowTransparencyLevel.Mica };
    private static readonly WindowTransparencyLevel[] DefaultHint = { WindowTransparencyLevel.None };

    private static readonly IBrush LightTint = new SolidColorBrush(Color.FromArgb(0x7A, 0xEA, 0xF0, 0xFB));
    private static readonly IBrush DarkTint = new SolidColorBrush(Color.FromArgb(0x7A, 0x1A, 0x1F, 0x2A));

    /// <summary>登记窗口，并按当前配置应用一次。</summary>
    public static void Attach(Window window)
    {
        if (!Windows.Contains(window))
            Windows.Add(window);

        window.Closed += (_, _) => Windows.Remove(window);
        Apply(window);
    }

    public static void ApplyToAll()
    {
        foreach (var window in Windows.ToArray())
            Apply(window);
    }

    private static void Apply(Window window)
    {
        if (UiOptions.Mica)
        {
            var tint = UiOptions.IsDark ? DarkTint : LightTint;
            window.TransparencyLevelHint = MicaHint;
            window.Resources ??= new ResourceDictionary();
            window.Resources["WindowBackgroundBrush"] = tint;
            window.Resources["PageBackgroundBrush"] = tint;
        }
        else
        {
            window.Resources?.Remove("WindowBackgroundBrush");
            window.Resources?.Remove("PageBackgroundBrush");
            window.TransparencyLevelHint = DefaultHint;
        }
    }
}
