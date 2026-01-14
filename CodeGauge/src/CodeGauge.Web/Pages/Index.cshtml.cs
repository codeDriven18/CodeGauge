using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeGauge.Core.Models;
using CodeGauge.Web.Services;

namespace CodeGauge.Web.Pages;

public class IndexModel : PageModel
{
    private readonly AnalysisService _analysisService;
    private readonly PdfGenerator _pdfGenerator;
    private static ScoreResult? _cachedResult;

    public IndexModel(AnalysisService analysisService, PdfGenerator pdfGenerator)
    {
        _analysisService = analysisService;
        _pdfGenerator = pdfGenerator;
    }

    public ScoreResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public string BadgeColor => Result?.SummaryBadge.Color switch
    {
        "green" => "success",
        "yellow" => "warning",
        _ => "danger"
    };

    public void OnGet()
    {
        Result = _cachedResult;
    }

    public async Task<IActionResult> OnPostAsync(string repoUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                ErrorMessage = "Please enter a repository URL";
                return Page();
            }

            var progress = new Progress<string>(msg => Console.WriteLine(msg));
            Result = await _analysisService.AnalyzeRepositoryAsync(repoUrl, progress);
            _cachedResult = Result;
            
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Analysis failed: {ex.Message}";
            return Page();
        }
    }

    public IActionResult OnGetDownloadPdf()
    {
        if (_cachedResult == null)
            return RedirectToPage();

        var pdfBytes = _pdfGenerator.GeneratePdf(_cachedResult);
        var fileName = $"codegauge-{_cachedResult.RepoName}-{DateTime.UtcNow:yyyyMMdd}.pdf";
        
        return File(pdfBytes, "application/pdf", fileName);
    }
}
