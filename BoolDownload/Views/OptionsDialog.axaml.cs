using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BoolDownload.Services;

namespace BoolDownload.Views;

public partial class OptionsDialog : Window
{
    public OptionsDialog()
    {
        InitializeComponent();

        LanguageBox.ItemsSource = new List<string>
        {
            Localization.LanguageLabel(AppLanguage.FollowSystem),
            Localization.LanguageLabel(AppLanguage.Chinese),
            Localization.LanguageLabel(AppLanguage.English),
        };
        LanguageBox.SelectedIndex = (int)UiOptions.Language;

        ThemeBox.ItemsSource = new List<string>
        {
            Localization.T("Loc_Theme_Light"),
            Localization.T("Loc_Theme_Dark"),
        };
        ThemeBox.SelectedIndex = UiOptions.IsDark ? 1 : 0;
        MicaBox.IsChecked = UiOptions.Mica;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        UiOptions.Save((AppLanguage)LanguageBox.SelectedIndex, ThemeBox.SelectedIndex == 1, MicaBox.IsChecked == true);
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
