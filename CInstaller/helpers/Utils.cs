using System.IO;
using System.IO.Compression;

namespace CInstaller;

public class Utils
{
    public static void CopyDirectory(string sourceDir, string destinationDir, ProgressReporter reporter)
    {
        var files = Directory.GetFiles(
            sourceDir,
            "*",
            SearchOption.AllDirectories);

        long totalBytes = files.Sum(f => new FileInfo(f).Length);
        long copiedBytes = 0;

        Directory.CreateDirectory(destinationDir);

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string destinationFile = Path.Combine(destinationDir, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

            File.Copy(file, destinationFile, true);

            copiedBytes += new FileInfo(file).Length;

            double percent = copiedBytes * 100.0 / totalBytes;

            reporter.Report(percent, "Mod Install wird erstellt");
        }
    }
    
    public static void ExtractZip(string zipPath, string extractPath, ProgressReporter progress)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        var roots = archive.Entries
            .Select(e => e.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(x => x != null)
            .Distinct()
            .ToList();

        var singleRootFolder =
            roots.Count == 1 &&
            archive.Entries.All(e => e.FullName.StartsWith(roots[0] + "/"));

        var total = archive.Entries.Count;
        var current = 0;
        
        foreach (var entry in archive.Entries)
        {
            current++;
            
            var relativePath = entry.FullName;

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
            
            var percent = (double)current / total * 100;
            progress.Report(percent, $"Extracting {entry.Name}");
        }
    }
}