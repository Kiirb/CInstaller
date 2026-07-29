using System.IO;
using System.IO.Compression;
using CInstaller.entities;

namespace CInstaller.helpers;

public abstract class Utils
{
    public static void CopyDirectoryWithoutBepInEx(string sourceDir, string destinationDir, ProgressReporter reporter)
    {
        string[] files = Directory.GetFiles(
            sourceDir,
            "*",
            SearchOption.AllDirectories);

        long totalBytes = files.Sum(f => new FileInfo(f).Length);
        long copiedBytes = 0;
        List<string> bepInExFiles = new(){ "winhttp.dll", "doorstop_config.ini", "changelog.txt", ".doorstop_version"};

        Directory.CreateDirectory(destinationDir);

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string destinationFile = Path.Combine(destinationDir, relativePath);
            
            if (bepInExFiles.Contains(relativePath) || relativePath.Split(Path.DirectorySeparatorChar)[0].Equals("BepInEx")) continue;
            
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

            File.Copy(file, destinationFile, true);

            copiedBytes += new FileInfo(file).Length;

            double percent = copiedBytes * 100.0 / totalBytes;

            reporter.Report(percent, "Mod Install wird erstellt");
        }
    }
    
    public static void ExtractZip(string zipPath, string extractPath, ProgressReporter progress)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        List<string?> roots = archive.Entries
            .Select(e => e.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(x => x != null)
            .Distinct()
            .ToList();

        bool singleRootFolder =
            roots.Count == 1 &&
            archive.Entries.All(e => e.FullName.StartsWith(roots[0] + "/"));

        int total = archive.Entries.Count;
        int current = 0;
        
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            current++;
            
            string relativePath = entry.FullName;

            if (singleRootFolder)
            {
                // Remove the root folder from the path
                relativePath = relativePath[roots[0]!.Length..].TrimStart('/');
            }

            if (string.IsNullOrEmpty(relativePath))
                continue;

            string destinationPath = Path.Combine(extractPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (!string.IsNullOrEmpty(entry.Name)) // skip directories
            {
                entry.ExtractToFile(destinationPath, true);
            }
            
            double percent = (double)current / total * 100;
            progress.Report(percent, $"Extracting {entry.Name}");
        }
    }
}