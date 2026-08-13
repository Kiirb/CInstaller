using System.Diagnostics;
using System.IO;

namespace CInstaller;

public class Loader
{
    public void LoaderRun()
    {
        string gameFolder = AppContext.BaseDirectory;
        string gameExe = Path.Join(gameFolder, Installer.GameName + ".exe");

        if (Path.Exists(gameExe))
        {
            Process.Start(gameExe);
        }
    }
}