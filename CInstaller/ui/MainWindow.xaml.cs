using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CInstaller;

public partial class MainWindow
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    
    public MainWindow()
    {
        InitializeComponent();
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
        var hwnd = new WindowInteropHelper(this).Handle;

        var useDark = 1;

        DwmSetWindowAttribute(
            hwnd,
            DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref useDark,
            sizeof(int));
    }
    
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        EnableDarkTitleBar();
        
        await Task.Yield();
        
        if (await Updater.CheckForUpdate())
        {
            //TODO Display current and new version in MessageBox
            
            var updateFlag = MessageBox.Show(
                "update Gefunden, willst du jetzt updaten?",
                "Hard Cleanup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (updateFlag.Equals(MessageBoxResult.Yes))
            {
                Updater.RunUpdate();
            }
        }
        
        var reporter = new ProgressReporter(progressBar, statusLabel);

        var steamPath = Installer.FindSteamPath();
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
        
        
        (var steamCommon,var gameFolder) = Installer.FindGameFolder(steamPath);

        if (string.IsNullOrEmpty(steamCommon) || string.IsNullOrEmpty(gameFolder))
        {
            var installGameFlag = MessageBox.Show(
                "Among us ist nicht installiert, installiere es und starte den Installer neu",
                "Not installed",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (installGameFlag.Equals(MessageBoxResult.OK))
            {
                await Installer.InstallGame(steamPath);
            }

            Application.Current.Shutdown();
            return;
        }
        
        var cleanupFlag = MessageBox.Show(
            "Sollen deine existierenden Among Us Installs aufgeräumt und neu installiert werden?",
            "Hard Cleanup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        
        if (cleanupFlag.Equals(MessageBoxResult.Yes))
        {
            reporter.Report(0, "Wartet auf game reinstall");
            await Installer.CleanUpGameFiles(steamPath, steamCommon);
        }
        
        reporter.Report(0, "Starte Installer");
        var moddedFolder = await Installer.RunInstaller(reporter, steamCommon, gameFolder);
        if (string.IsNullOrEmpty(moddedFolder))
        {
            MessageBox.Show(
                "Plugin Ordner konnte nicht gefunden werden",
                "Install Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        
        var restartSteamFlag = await Installer.AddToSteam(steamPath, moddedFolder);
        
        statusLabel.Content = "Installation complete!";
        progressBar.Value = 100;

        if (restartSteamFlag && SteamManager.IsSteamRunning())
        {
            var result = MessageBox.Show(
                "Steam Muss neu gestartet werden um alle änderungen zu übernehmen\n\nJetzt neustarten?",
                "Confirmation",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.OK)
            {
                await Installer.RestartSteam(steamPath);
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