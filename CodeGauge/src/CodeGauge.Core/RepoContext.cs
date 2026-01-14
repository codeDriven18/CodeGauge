using LibGit2Sharp;

namespace CodeGauge.Core;

public class RepoContext
{
    public string RootPath { get; private set; } = string.Empty;
    public bool Verbose { get; private set; }
    public string? SolutionPath { get; private set; }
    public IReadOnlyList<string> CsprojFiles { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> CSharpFiles { get; private set; } = Array.Empty<string>();
    public Repository? GitRepo { get; private set; }

    public static RepoContext Create(string path, bool verbose)
    {
        var ctx = new RepoContext
        {
            RootPath = Path.GetFullPath(path),
            Verbose = verbose,
        };

        // Discover solution
        var sln = Directory.GetFiles(ctx.RootPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        ctx.SolutionPath = sln;

        // Discover projects and C# files
        ctx.CsprojFiles = Directory.GetFiles(ctx.RootPath, "*.csproj", SearchOption.AllDirectories);
        ctx.CSharpFiles = Directory.GetFiles(ctx.RootPath, "*.cs", SearchOption.AllDirectories);

        // Git repository
        try
        {
            string? gitRoot = Repository.Discover(ctx.RootPath);
            if (gitRoot != null)
            {
                ctx.GitRepo = new Repository(gitRoot);
            }
        }
        catch
        {
            ctx.GitRepo = null;
        }

        return ctx;
    }
}
