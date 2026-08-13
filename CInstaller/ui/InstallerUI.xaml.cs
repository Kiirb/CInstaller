using System.IO;
using System.Windows;
using CInstaller.helpers;

namespace CInstaller.ui;

public partial class InstallerUI
{
    public async Task InstallerUIRun()
    {
        string? steamPath = Installer.FindSteamPath();
        if (string.IsNullOrEmpty(steamPath))
        {
            MessageBox.Show(
                "Steam konnte nicht gefunden werden",
                "Find Path Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
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
        }

        LoadingMenu loadingMenu = new();
        loadingMenu.Show();

        loadingMenu.Progress.Report(0, "Starte Installer");
        string moddedFolder = await Installer.RunInstaller(loadingMenu.Progress, steamCommon);
        if (string.IsNullOrEmpty(moddedFolder))
        {
            MessageBox.Show(
                "Plugin Ordner konnte nicht gefunden werden",
                "Install Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        
        bool restartSteamFlag = await Installer.AddToSteam(steamPath, moddedFolder, loadingMenu.Progress);
        
        loadingMenu.Progress.Complete("Installation complete!");

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
