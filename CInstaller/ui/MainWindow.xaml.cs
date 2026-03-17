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

        int useDark = 1;

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

        var steamCommon = Installer.FindSteamCommon();
        if (string.IsNullOrEmpty(steamCommon))
        {
            var result = MessageBox.Show(
                "Steam oder Among us konnte nicht gefunden werden",
                "Find Path Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        
        var cleanupFlag = MessageBox.Show(
            "Sollen deine existierenden Among Us Installs aufgeräumt und neu installiert werden?",
            "Hard Cleanup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (cleanupFlag.Equals(MessageBoxResult.Yes))
        {
            Installer.HardCleanFlag = true;
        }
        
        await Task.Run(() => Installer.RunInstaller(reporter, steamCommon));

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