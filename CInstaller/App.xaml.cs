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

        //await CheckUpdate();
        
        if (IsLoaderMode())
        {
            new Loader().LoaderRun();
        }
        else
        {
            await new InstallerUi().InstallerUiRun();
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
                ProgressUI progressUi = new();
                progressUi.Show();
                await Updater.RunUpdate(progressUi.Progress);
                Current.Shutdown();
            }
        }
    }

    private static bool IsLoaderMode()
    {
        string exeDir = Path.GetDirectoryName(Environment.ProcessPath)!;
        return File.Exists(Path.Join(exeDir, Installer.GameName + ".exe"));
    }
}