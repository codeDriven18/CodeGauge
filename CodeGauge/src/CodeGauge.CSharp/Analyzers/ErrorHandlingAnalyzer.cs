using CodeGauge.Core.Interfaces;
using CodeGauge.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeGauge.CSharp.Analyzers;

public class ErrorHandlingAnalyzer : IAnalyzer
{
    public string Name => "error_handling";

    public Task<(int score, IEnumerable<FeedbackItem> feedback)> AnalyzeAsync(CodeGauge.Core.RepoContext context)
    {
        var feedback = new List<FeedbackItem>();
        int max = Weights.ErrorHandling;
        int tryCatchCount = 0;
        int nullChecks = 0;
        int riskyCalls = 0;

        foreach (var file in context.CSharpFiles)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();
            tryCatchCount += root.DescendantNodes().OfType<TryStatementSyntax>().Count();
            nullChecks += root.DescendantNodes().OfType<BinaryExpressionSyntax>().Count(b => b.Kind() == SyntaxKind.EqualsExpression || b.Kind() == SyntaxKind.NotEqualsExpression);

            // Heuristic: API calls without checks (very rough)
            riskyCalls += root.DescendantNodes().OfType<InvocationExpressionSyntax>().Count();
        }

        int score = max;
        if (tryCatchCount == 0 && riskyCalls > 10) score -= 5;
        if (nullChecks < Math.Max(1, riskyCalls / 20)) score -= 3;
        score = Math.Max(0, score);

        if (tryCatchCount == 0)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = score,
                Issue = "No try/catch blocks detected in codebase.",
                Recommendation = "Add exception handling with logging where IO/serialization/network operations occur."
            });
        }
        if (score < max)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = score,
                Issue = "Insufficient null checks or defensive coding.",
                Recommendation = "Use null guards, argument validation, and `Nullable` reference types."
            });
        }
        return Task.FromResult((score, feedback.AsEnumerable()));
    }
}
