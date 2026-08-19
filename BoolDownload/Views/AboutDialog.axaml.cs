using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BoolDownload.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is not null ? version.ToString(3) : "1.0.0";

        VersionText.Text = $"版本 {versionText}";
        SoftwareVersionText.Text = versionText;
        CopyrightText.Text = $"Copyright © {DateTime.Now.Year}";
        LicenseText.Text = "GPL-3.0 license";
        EngineText.Text = "Xunlei Open Download SDK";

        OsText.Text = $"{RuntimeInformation.OSDescription}";
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;
        ArchitectureText.Text = $"{RuntimeInformation.ProcessArchitecture} ({RuntimeInformation.OSArchitecture})";
        BaseDirectoryText.Text = AppContext.BaseDirectory;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}