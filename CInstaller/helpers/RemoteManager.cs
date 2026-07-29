using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CInstaller.entities;

namespace CInstaller.helpers;

public static class RemoteManager
{
    private static readonly HttpClient HttpClient = new();
    private static readonly HttpClient ConfigHttpClient = new();
    private const string GithubToken = "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";
    private const string ConfigUrl = "https://gist.github.com/Kiirb/b96f6b5f2268f239fc387222aa3795be/raw";
    
    public static async Task<HttpResponseMessage> FindLatestGithubRelease(string repoOwner, string repoName)
    {
        string githubUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases";

        if (HttpClient.DefaultRequestHeaders.Authorization == null)
        {
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GithubToken);
            HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Updater");
        }
        
        HttpResponseMessage response = await HttpClient.GetAsync(githubUrl);

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

        string json = await response.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement assets = doc.RootElement[0].GetProperty("assets");

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            string? url = asset.GetProperty("url").GetString();

            if (name != null && name.Contains(searchPattern))
                return url!;
        }

        throw new Exception("No matching asset found.");
    }


    public static async Task<string> DownloadFile(string url, string outputPath, ProgressReporter progress)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/octet-stream");

        HttpResponseMessage response = await HttpClient.SendAsync(request);

        long totalBytes = response.Content.Headers.ContentLength ?? -1L;

        string filename = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? Path.GetFileName(url);
        string outputFile = Path.Join(outputPath, filename);

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        await using FileStream file = File.Create(outputFile);

        byte[] buffer = new byte[8192];
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

    public static async Task<List<GithubRepo>> GetPluginConfig()
    {
        string json = await ConfigHttpClient.GetStringAsync(ConfigUrl);

        List<GithubRepo>? repos = JsonSerializer.Deserialize<List<GithubRepo>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (repos == null || repos.Count == 0)
            throw new Exception("Plugin config JSON was empty or invalid.");

        return repos;
    }
}