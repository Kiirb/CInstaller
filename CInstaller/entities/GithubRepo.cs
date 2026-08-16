using System.Data;

namespace CInstaller.entities;

public class GithubRepo(string repoOwner, string repoName, string searchPattern = ".dll", string filePath = "", Version? version = null)
{
    public string RepoOwner { get; } = repoOwner;
    public string RepoName { get; } = repoName;
    public string SearchPattern { get; } = searchPattern;
    public string filePath { get; set; } = filePath;
    public Version version { get; set; } = version;
}