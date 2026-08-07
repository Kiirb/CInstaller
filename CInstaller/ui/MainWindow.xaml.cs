using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CInstaller.entities;
using CInstaller.helpers;

namespace CInstaller.ui;

public partial class MainWindow
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        Loaded += MainWindow_Loaded;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attr,
        ref int attrValue,
        int attrSize);

    private void EnableDarkTitleBar()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        int useDark = 1;

        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }

    private static async Task CheckUpdate(ProgressReporter progress)
    {
        Version currentVersion = Updater.GetCurrentVersion();
        Version? latestVersion = await Updater.GetLatestVersion();
        
        if (latestVersion > currentVersion)
        {
            MessageBoxResult updateFlag = MessageBox.Show(
                $"Jetzige Version: {currentVersion}\nNeuste Version: {latestVersion}.0\n\nJetzt updaten?",
                "Update Gefunden",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (updateFlag.Equals(MessageBoxResult.Yes))
            {
                await Updater.RunUpdate(progress);
                Application.Current.Shutdown();
            }
        }
    }
    
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ProgressReporter progress = new(progressBar, statusLabel);
        
        await CheckUpdate(progress);
        
        string? steamPath = Installer.FindSteamPath();
        if (string.IsNullOrEmpty(steamPath))
        {
            MessageBox.Show(
                "Steam konnte nicht gefunden werden",
                "Find Path Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Application.Current.Shutdown();
            return;
        }
        
        string steamCommon = Installer.FindSteamCommon(steamPath);

        if (string.IsNullOrEmpty(steamCommon) || !Directory.Exists(Path.Join(steamCommon, Installer.GameName)))
        {
            MessageBoxResult installGameFlag = MessageBox.Show(
                "Among us ist nicht installiert, installiere es und starte den Installer neu",
                "Not installed",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (installGameFlag.Equals(MessageBoxResult.OK))
            {
                await SteamManager.InstallGame(steamPath, Installer.SteamGameId);
            }

            Application.Current.Shutdown();
            return;
        }
        
        progress.Report(0, "Starte Installer");
        string moddedFolder = await Installer.RunInstaller(progress, steamCommon);
        if (string.IsNullOrEmpty(moddedFolder))
        {
            MessageBox.Show(
                "Plugin Ordner konnte nicht gefunden werden",
                "Install Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        
        bool restartSteamFlag = await Installer.AddToSteam(steamPath, moddedFolder, progress);
        
        statusLabel.Content = "Installation complete!";
        progressBar.Value = 100;

        if (restartSteamFlag && SteamManager.IsSteamRunning())
        {
            MessageBoxResult result = MessageBox.Show(
                "Steam Muss neu gestartet werden um alle änderungen zu übernehmen\n\nJetzt neustarten?",
                "Confirmation",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.OK)
            {
                await SteamManager.RestartSteam(steamPath);
            }
        }
        else
        {
            MessageBox.Show(
                "Installation Fertig!",
                "Install Done",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        
        Application.Current.Shutdown();
    }
}