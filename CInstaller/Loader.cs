using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CInstaller;

public class Loader
{
    public void LoaderRun()
    {
        string gameFolder = Path.GetDirectoryName(Environment.ProcessPath)!;
        string gameExe = Path.Join(gameFolder, Installer.GameName + ".exe");

        if (Path.Exists(gameExe))
        {
            Process.Start(gameExe);
        }
        Application.Current.Shutdown();
    }
}