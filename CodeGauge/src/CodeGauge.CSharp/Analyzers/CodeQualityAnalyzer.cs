using CodeGauge.Core.Interfaces;
using CodeGauge.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeGauge.CSharp.Analyzers;

public class CodeQualityAnalyzer : IAnalyzer
{
    public string Name => "code_quality";

    public Task<(int score, IEnumerable<FeedbackItem> feedback)> AnalyzeAsync(CodeGauge.Core.RepoContext context)
    {
        var feedback = new List<FeedbackItem>();
        int max = Weights.CodeQuality;
        int totalComplexity = 0;
        int methodCount = 0;
        int longMethods = 0;
        int deepNesting = 0;
        int duplicationHeuristic = 0;

        foreach (var file in context.CSharpFiles)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var m in methods)
            {
                methodCount++;
                int complexity = 1;
                complexity += m.DescendantNodes().OfType<IfStatementSyntax>().Count();
                complexity += m.DescendantNodes().OfType<ForStatementSyntax>().Count();
                complexity += m.DescendantNodes().OfType<ForEachStatementSyntax>().Count();
                complexity += m.DescendantNodes().OfType<WhileStatementSyntax>().Count();
                complexity += m.DescendantNodes().OfType<SwitchStatementSyntax>().SelectMany(s => s.Sections).Sum(sec => sec.Labels.Count);
                complexity += m.DescendantNodes().OfType<CatchClauseSyntax>().Count();
                totalComplexity += complexity;

                // Long methods: >50 lines
                var span = m.Span;
                var startLine = tree.GetLineSpan(span).StartLinePosition.Line;
                var endLine = tree.GetLineSpan(span).EndLinePosition.Line;
                if (endLine - startLine + 1 > 50) longMethods++;

                // Nesting depth
                int maxDepth = GetMaxBlockDepth(m.Body ?? m.ExpressionBody as CSharpSyntaxNode);
                if (maxDepth > 3) deepNesting++;
            }

            // Duplication heuristic: repeated 5+ line blocks appearing more than once (very rough)
            var lines = text.Split('\n');
            var hashes = new Dictionary<string, int>();
            for (int i = 0; i + 5 < lines.Length; i++)
            {
                var block = string.Join('\n', lines.Skip(i).Take(5)).Trim();
                if (block.Length < 40) continue;
                var h = block.GetHashCode();
                var key = h.ToString();
                hashes[key] = hashes.TryGetValue(key, out var cnt) ? cnt + 1 : 1;
            }
            duplicationHeuristic += hashes.Values.Count(v => v > 1);
        }

        // Map metrics to score
        double avgComplexity = methodCount > 0 ? totalComplexity / (double)methodCount : 0;
        int score = max;
        if (avgComplexity > 10) score -= 5;
        if (longMethods > 0) score -= Math.Min(10, longMethods);
        if (deepNesting > 0) score -= Math.Min(5, deepNesting);
        if (duplicationHeuristic > 0) score -= Math.Min(5, duplicationHeuristic);
        score = Math.Max(0, score);

        if (longMethods > 0)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = score,
                Issue = $"{longMethods} method(s) exceed 50 lines.",
                Recommendation = "Refactor into smaller helpers and reduce responsibilities."
            });
        }
        if (deepNesting > 0)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = score,
                Issue = $"{deepNesting} method(s) have nesting depth >3.",
                Recommendation = "Early returns, guard clauses, or strategy patterns to flatten logic."
            });
        }
        if (duplicationHeuristic > 0)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = score,
                Issue = "Duplicate code blocks detected (heuristic).",
                Recommendation = "Extract reusable functions or shared classes to follow DRY."
            });
        }

        return Task.FromResult((score, feedback.AsEnumerable()));
    }

    private static int GetMaxBlockDepth(CSharpSyntaxNode? node)
    {
        if (node == null) return 0;
        int max = 0;
        void Walk(SyntaxNode n, int d)
        {
            max = Math.Max(max, d);
            foreach (var child in n.ChildNodes())
            {
                Walk(child, d + (child is BlockSyntax ? 1 : 0));
            }
        }
        Walk(node, 0);
        return max;
    }
}
