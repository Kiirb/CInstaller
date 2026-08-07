namespace CInstaller.entities;

public class GithubRepo(string repoOwner, string repoName, string searchPattern = ".dll")
{
    public string RepoOwner { get; } = repoOwner;
    public string RepoName { get; } = repoName;
    public string SearchPattern { get; } = searchPattern;
}