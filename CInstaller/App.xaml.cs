using System.IO;
using System.Windows;
using CInstaller.helpers;
using CInstaller.ui;

namespace CInstaller;

public partial class App
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await CheckUpdate();

        if (IsLoaderMode())
        {
            await Loader.LoaderRun();
        }
        else
        {
            await Installer.InstallerRun();
        }
    }
    
    private static async Task CheckUpdate()
    {
        Version currentVersion = Updater.GetCurrentVersion();
        Version? latestVersion;
        try
        {
            latestVersion = await Updater.GetLatestVersion();
        }
        catch (Exception e)
        {
            return;
        }
        
        if (latestVersion > currentVersion)
        {
            MessageBoxResult updateFlag = MessageBox.Show(
                $"Jetzige Version: {currentVersion}\nNeuste Version: {latestVersion}\n\nJetzt updaten?",
                "CInstaller hat ein update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (updateFlag.Equals(MessageBoxResult.Yes))
            {
                ProgressUI progressUi = new();
                progressUi.Show();
                await Updater.RunUpdate(progressUi.Progress);
                Current.Shutdown();
            }
        }
    }

    private static bool IsLoaderMode()
    { 
        return File.Exists(ConfigHandler.ConfigPath);
    }
}