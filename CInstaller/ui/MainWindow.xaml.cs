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
        
        var reporter = new ProgressReporter(progressBar, statusLabel);

        var steamCommon = await Installer.FindSteamCommon();
        if (string.IsNullOrEmpty(steamCommon))
        {
            MessageBox.Show(
                "Steam oder Steam Common konnte nicht gefunden werden",
                "Find Path Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Application.Current.Shutdown();
        }
        
        var gameFolder = await Installer.FindGameFolder(steamCommon);

        var isInstalled = true;//!string.IsNullOrEmpty(gameFolder);

        const string baseText = "Sollen deine existierenden Among Us Installs aufgeräumt und neu installiert werden?";
        const string notInstalledText = "Among us ist nicht installiert, solle es installiet werden?";
        
        var cleanupFlag = MessageBox.Show(
            isInstalled ? baseText : notInstalledText,
            "Hard Cleanup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (cleanupFlag.Equals(MessageBoxResult.No) && !isInstalled)
        {
            Application.Current.Shutdown();
        }
        else
        {
            Installer.HardCleanFlag = true;
        }
        
        await Installer.RunInstaller(reporter, steamCommon, gameFolder);

        statusLabel.Content = "Installation complete!";
        progressBar.Value = 100;

        if (Installer.RestartSteamFlag)
        {
            var result = MessageBox.Show(
                "Steam Muss neu gestartet werden um alle änderungen zu übernehmen\n\nJetzt neustarten?",
                "Confirmation",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.OK)
            {
                Installer.RestartSteam();
            }
        }
        else
        {
            MessageBox.Show(
                "Installation completed successfully!",
                "Installer",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        
        Application.Current.Shutdown();
    }
}