using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CInstaller;

public class GithubManager
{
    private static readonly HttpClient HttpClient = new();
    private const string GithubToken = "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";
    
    public static async Task<HttpResponseMessage> FindLatestGithubRelease(string repoOwner, string repoName)
    {
        var githubUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases";

        if (HttpClient.DefaultRequestHeaders.Authorization == null)
        {
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GithubToken);
            HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Updater");
        }
        
        var response = await HttpClient.GetAsync(githubUrl);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset))
            {
                var resetTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reset.First()));
                throw new Exception($"GitHub rate limit exceeded. Try again at {resetTime.LocalDateTime}.");
            }
        }

        response.EnsureSuccessStatusCode();
        
        return response;
    }
    
    public static async Task<string> FindLatestGithubDownloadAsset(string repoOwner, string repoName, string searchPattern, HttpResponseMessage? response = null)
    {
        response ??= await FindLatestGithubRelease(repoOwner, repoName);

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var assets = doc.RootElement[0].GetProperty("assets");

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("url").GetString();

            if (name != null && name.Contains(searchPattern))
                return url!;
        }

        throw new Exception("No matching asset found.");
    }


    public static async Task<string> DownloadFile(string url, string outputPath, ProgressReporter? progress)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/octet-stream");

        var response = await HttpClient.SendAsync(request);

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        var filename = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? Path.GetFileName(url);
        var outputFile = Path.Join(outputPath, filename);

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = File.Create(outputFile);

        var buffer = new byte[8192];
        long totalRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (totalBytes > 0)
            {
                double percent = (double)totalRead / totalBytes * 100;
                progress.Report(percent, $"Downloading {filename}");
            }
        }

        return outputFile;
    }
}