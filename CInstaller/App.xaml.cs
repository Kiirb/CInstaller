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
        
        if (isLoaderMode())
        {
            new Loader().LoaderRun();
        }
        else
        {
            await new InstallerUI().InstallerUIRun();
        }
    }
    
    private static async Task CheckUpdate()
    {
        Version currentVersion = Updater.GetCurrentVersion();
        Version? latestVersion = await Updater.GetLatestVersion();
        
        if (latestVersion > currentVersion)
        {
            MessageBoxResult updateFlag = MessageBox.Show(
                $"Jetzige Version: {currentVersion}\nNeuste Version: {latestVersion}.0\n\nJetzt updaten?",
                "CInstaller hat ein update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (updateFlag.Equals(MessageBoxResult.Yes))
            {
                LoadingMenu loadingMenu = new();
                loadingMenu.Show();
                await Updater.RunUpdate(loadingMenu.Progress);
                Current.Shutdown();
            }
        }
    }

    private static bool isLoaderMode()
    {
        string exeDir = AppContext.BaseDirectory;
        return File.Exists(Path.Join(exeDir, Installer.GameName + ".exe"));
    }
}