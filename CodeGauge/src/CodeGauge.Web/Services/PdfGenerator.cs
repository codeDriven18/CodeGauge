using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CodeGauge.Core.Models;

namespace CodeGauge.Web.Services;

public class PdfGenerator
{
    public byte[] GeneratePdf(ScoreResult result)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("CodeGauge Report").FontSize(24).Bold();
                            col.Item().Text($"Repository: {result.RepoName}").FontSize(14);
                            col.Item().Text($"Generated: {result.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC").FontSize(10).Italic();
                        });

                        row.ConstantItem(100).AlignRight().Column(col =>
                        {
                            var color = result.SummaryBadge.Color switch
                            {
                                "green" => Colors.Green.Medium,
                                "yellow" => Colors.Orange.Medium,
                                _ => Colors.Red.Medium
                            };
                            
                            col.Item().Background(color).Padding(10).Text($"{result.TotalScore}/100")
                                .FontSize(20).Bold().FontColor(Colors.White);
                            col.Item().PaddingTop(5).Text(result.SummaryBadge.Color.ToUpper())
                                .FontSize(12).Bold().AlignCenter();
                        });
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(col =>
                    {
                        // Category Scores
                        col.Item().Text("Category Scores").FontSize(18).Bold();
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Category").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Score").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Max").Bold();
                            });

                            AddScoreRow(table, "Tests & Coverage", result.CategoryScores.Tests, Weights.TestsCoverage);
                            AddScoreRow(table, "Code Quality", result.CategoryScores.CodeQuality, Weights.CodeQuality);
                            AddScoreRow(table, "Commit Hygiene", result.CategoryScores.CommitHygiene, Weights.CommitHygiene);
                            AddScoreRow(table, "Documentation", result.CategoryScores.Documentation, Weights.Documentation);
                            AddScoreRow(table, "Dependencies", result.CategoryScores.Dependencies, Weights.Dependencies);
                            AddScoreRow(table, "Error Handling", result.CategoryScores.ErrorHandling, Weights.ErrorHandling);
                            AddScoreRow(table, "Lint & Formatting", result.CategoryScores.LintFormatting, Weights.LintFormatting);
                        });

                        // Feedback
                        col.Item().PaddingTop(20).Text("Detailed Feedback").FontSize(18).Bold();
                        
                        foreach (var feedback in result.Feedback)
                        {
                            col.Item().PaddingTop(10).Column(feedbackCol =>
                            {
                                feedbackCol.Item().Text($"[{feedback.Metric}]").FontSize(12).Bold().FontColor(Colors.Blue.Medium);
                                feedbackCol.Item().PaddingTop(2).Text($"Issue: {feedback.Issue}").FontSize(10);
                                feedbackCol.Item().PaddingTop(2).Text($"→ {feedback.Recommendation}").FontSize(10).Italic().FontColor(Colors.Green.Darken2);
                            });
                        }

                        // Developer Tier
                        col.Item().PaddingTop(20).Text("Developer Readiness Tier").FontSize(18).Bold();
                        col.Item().PaddingTop(5).Text(GetTierText(result.TotalScore)).FontSize(14);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Generated by ").FontSize(9);
                        x.Span("CodeGauge").FontSize(9).Bold();
                        x.Span(" - Automated Code Readiness Scoring").FontSize(9);
                    });
            });
        }).GeneratePdf();
    }

    private void AddScoreRow(TableDescriptor table, string category, int score, int max)
    {
        var percentage = max > 0 ? (score * 100.0 / max) : 0;
        var color = percentage >= 80 ? Colors.Green.Lighten3 :
                    percentage >= 60 ? Colors.Yellow.Lighten3 :
                    Colors.Red.Lighten3;

        table.Cell().Background(color).Padding(5).Text(category);
        table.Cell().Background(color).Padding(5).Text(score.ToString());
        table.Cell().Background(color).Padding(5).Text(max.ToString());
    }

    private string GetTierText(int score)
    {
        return score switch
        {
            >= 91 => "🌟 Elite (91-100): Exemplary code quality",
            >= 76 => "🟢 Senior (76-90): Production-ready with best practices",
            >= 51 => "🟡 Mid (51-75): Good baseline, room for refinement",
            _ => "🔴 Junior (0-50): Needs foundational improvements"
        };
    }
}
