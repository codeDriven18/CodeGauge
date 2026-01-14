using System.Diagnostics;
using CodeGauge.Core;
using CodeGauge.Core.Engine;
using CodeGauge.Core.Models;

namespace CodeGauge.Web.Services;

public class AnalysisService
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "codegauge-analysis");

    public async Task<ScoreResult> AnalyzeRepositoryAsync(string repoUrl, IProgress<string>? progress = null)
    {
        progress?.Report("Cloning repository...");
        
        // Create temp directory
        var repoId = Guid.NewGuid().ToString("N");
        var repoPath = Path.Combine(_tempPath, repoId);
        Directory.CreateDirectory(repoPath);

        try
        {
            // Clone repo using git
            var cloneProcess = new ProcessStartInfo("git", $"clone --depth 1 {repoUrl} \"{repoPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(cloneProcess);
            if (proc == null)
                throw new Exception("Failed to start git clone");

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(120000)) // 2 min timeout
            {
                proc.Kill();
                throw new TimeoutException("Repository clone timed out");
            }

            if (proc.ExitCode != 0)
            {
                var error = await errorTask;
                throw new Exception($"Git clone failed: {error}");
            }

            progress?.Report("Analyzing repository...");

            // Run analysis
            var ctx = RepoContext.Create(repoPath, verbose: false);
            var engine = new AnalysisEngine(new List<CodeGauge.Core.Interfaces.IAnalyzer>
            {
                new CodeGauge.Core.Analyzers.TestsAnalyzer(),
                new CodeGauge.CSharp.Analyzers.CodeQualityAnalyzer(),
                new CodeGauge.Core.Analyzers.CommitAnalyzer(),
                new CodeGauge.Core.Analyzers.DocumentationAnalyzer(),
                new CodeGauge.Core.Analyzers.DependencyAnalyzer(),
                new CodeGauge.CSharp.Analyzers.ErrorHandlingAnalyzer(),
                new CodeGauge.Core.Analyzers.LintAnalyzer(),
            });

            var result = await engine.RunAsync(ctx);
            progress?.Report("Analysis complete!");
            return result;
        }
        finally
        {
            // Cleanup
            try
            {
                if (Directory.Exists(repoPath))
                    Directory.Delete(repoPath, recursive: true);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }
}
