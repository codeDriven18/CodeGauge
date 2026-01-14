using CodeGauge.Core.Engine;
using CodeGauge.Core;
using CodeGauge.Report;

namespace CodeGauge.CLI;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
		string repoPath = Directory.GetCurrentDirectory();
		string outJson = Path.Combine(Directory.GetCurrentDirectory(), "codegauge-report.json");
		string outMd = Path.Combine(Directory.GetCurrentDirectory(), "codegauge-report.md");
		bool verbose = false;

		for (int i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--path":
					if (i + 1 < args.Length) repoPath = args[++i];
					break;
				case "--out-json":
					if (i + 1 < args.Length) outJson = args[++i];
					break;
				case "--out-md":
					if (i + 1 < args.Length) outMd = args[++i];
					break;
				case "--verbose":
					verbose = true;
					break;
			}
		}

		var ctx = RepoContext.Create(repoPath, verbose);
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

		var json = JsonReporter.Render(result);
		File.WriteAllText(outJson, json);
		var md = MarkdownReporter.Render(result);
		File.WriteAllText(outMd, md);

		if (verbose)
		{
			Console.WriteLine($"JSON written to: {outJson}");
			Console.WriteLine($"Markdown written to: {outMd}");
		}
		return 0;
	}
}
