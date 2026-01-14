using CodeGauge.Core.Interfaces;
using CodeGauge.Core.Models;
using System.Text.RegularExpressions;
using System.Net.Http.Json;

namespace CodeGauge.Core.Analyzers;

public class DependencyAnalyzer : IAnalyzer
{
    public string Name => "dependencies";

    public async Task<(int score, IEnumerable<FeedbackItem> feedback)> AnalyzeAsync(RepoContext context)
    {
        var feedback = new List<FeedbackItem>();
        int max = Weights.Dependencies;

        var packages = new List<(string id, string version)>();
        foreach (var csproj in context.CsprojFiles)
        {
            var xml = File.ReadAllText(csproj);
            foreach (System.Xml.Linq.XElement pr in System.Xml.Linq.XDocument.Parse(xml).Descendants("PackageReference"))
            {
                var id = pr.Attribute("Include")?.Value ?? string.Empty;
                var ver = pr.Attribute("Version")?.Value ?? pr.Element("Version")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ver))
                    packages.Add((id, ver));
            }
        }

        if (!packages.Any())
        {
            return (score: (int)(max * 0.5), feedback);
        }

        // Check for outdated via NuGet API (best-effort)
        int outdated = 0;
        using var http = new HttpClient();
        foreach (var (id, version) in packages.Distinct())
        {
            try
            {
                var url = $"https://api.nuget.org/v3-flatcontainer/{id.ToLowerInvariant()}/index.json";
                var index = await http.GetFromJsonAsync<NugetIndex>(url);
                var latest = index?.Versions?.LastOrDefault();
                if (!string.IsNullOrEmpty(latest) && latest != version)
                {
                    outdated++;
                    feedback.Add(new FeedbackItem
                    {
                        Metric = Name,
                        Score = 0,
                        Issue = $"Outdated dependency: {id} {version} (latest {latest}).",
                        Recommendation = $"Update {id} to {latest} after compatibility testing."
                    });
                }
            }
            catch
            {
                // ignore network issues
            }
        }

        int score = Math.Max(0, max - Math.Min(max, outdated));
        return (score, feedback);
    }

    private class NugetIndex
    {
        public List<string>? Versions { get; set; }
    }
}
