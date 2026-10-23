using System;
using Avalonia;
using Avalonia.Styling;

namespace BoolDownload.Services;

/// <summary>
/// 界面选项：语言、主题色与毛玻璃效果，持久化于 ~/.booldownload/booldownload.db 的 option 表。
/// </summary>
public static class UiOptions
{
    private const string LanguageOption = "language";
    private const string ThemeOption = "theme";
    private const string MicaOption = "mica";

    public static AppLanguage Language { get; private set; } = AppLanguage.FollowSystem;

    public static bool IsDark { get; private set; }

    public static bool Mica { get; private set; }

    /// <summary>启动时读取配置并应用到界面。</summary>
    public static void LoadAndApply()
    {
        Language = ParseLanguage(OptionRepository.Get(LanguageOption));
        IsDark = string.Equals(OptionRepository.Get(ThemeOption), "dark", StringComparison.OrdinalIgnoreCase);
        Mica = string.Equals(OptionRepository.Get(MicaOption), "on", StringComparison.OrdinalIgnoreCase);

        Localization.Apply(Language);
        ApplyTheme();
    }

    public static void Save(AppLanguage language, bool dark, bool mica)
    {
        Language = language;
        IsDark = dark;
        Mica = mica;

        OptionRepository.Save(LanguageOption, LanguageToValue(language));
        OptionRepository.Save(ThemeOption, dark ? "dark" : "light");
        OptionRepository.Save(MicaOption, mica ? "on" : "off");

        Localization.Apply(language);
        ApplyTheme();
        MicaEffect.ApplyToAll();
    }

    private static void ApplyTheme()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private static AppLanguage ParseLanguage(string? value) => value switch
    {
        "zh" => AppLanguage.Chinese,
        "en" => AppLanguage.English,
        _ => AppLanguage.FollowSystem,
    };

    private static string LanguageToValue(AppLanguage language) => language switch
    {
        AppLanguage.Chinese => "zh",
        AppLanguage.English => "en",
        _ => "system",
    };
}
