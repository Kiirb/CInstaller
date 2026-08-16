using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CInstaller.entities;
using CInstaller.helpers;

namespace CInstaller.ui;

public partial class ProgressUI
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public ProgressReporter Progress { get; }
    
    public ProgressUI()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => EnableDarkTitleBar();

        Progress = new ProgressReporter(progressBar, statusLabel);
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
}