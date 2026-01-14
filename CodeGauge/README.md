# CodeGauge 📊

**Automated Code Readiness Scoring for .NET/C# Repositories**

![Score](https://img.shields.io/badge/Score-0--100-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)

---

## What is CodeGauge?

CodeGauge is an automated code analysis system that evaluates .NET/C# repositories and produces an objective **Code Readiness Score** (0–100) with detailed feedback. Designed for junior developers and teams, it provides actionable recommendations to improve code quality, testing, documentation, and development practices.

### Key Features

✅ **Automated Analysis** – Analyzes repos with zero manual configuration  
✅ **7 Scoring Categories** – Tests, code quality, commits, docs, dependencies, error handling, and lint  
✅ **Dual Output Formats** – JSON (machine-readable) and Markdown (human-readable)  
✅ **Actionable Feedback** – Every deduction includes plain-English explanations and recommendations  
✅ **Badge-Ready** – Color-coded summary (🟢 Green, 🟡 Yellow, 🔴 Red)  
✅ **CI/CD Integration** – Run on GitHub Actions, Azure Pipelines, or any CI system  
✅ **Extensible Architecture** – Modular design supports adding new languages and metrics

---

## Quick Start

### Prerequisites

- .NET SDK 10+ ([Download](https://dotnet.microsoft.com/download))
- Git (for web interface repository cloning)

### CLI Usage

```bash
# Analyze current directory
dotnet run --project src/CodeGauge.CLI

# Analyze specific repository
dotnet run --project src/CodeGauge.CLI -- --path /path/to/repo

# With custom output paths
dotnet run --project src/CodeGauge.CLI -- \
  --path /path/to/repo \
  --out-json report.json \
  --out-md report.md \
  --verbose
```

### 🌐 Web Interface (NEW!)

**Analyze any public repository with a browser:**

```bash
cd CodeGauge/CodeGauge
dotnet run --project src/CodeGauge.Web
```

Open `https://localhost:5001` in your browser:
1. Paste repository URL
2. Click "Analyze Repository"
3. View results on screen
4. Download PDF report

See [WEB-QUICKSTART.md](WEB-QUICKSTART.md) for deployment options.

### CLI Options

| Option | Description | Default |
|--------|-------------|---------|
| `--path` | Repository path to analyze | Current directory |
| `--out-json` | JSON output file | `codegauge-report.json` |
| `--out-md` | Markdown output file | `codegauge-report.md` |
| `--verbose` | Enable detailed logging | `false` |

---

## Scoring System

**Total Score: 0–100** (sum of weighted categories)

| Category | Weight | What It Measures |
|----------|--------|------------------|
| **Tests & Coverage** | 25 | Test presence, coverage %, passing tests, skipped tests |
| **Code Quality** | 25 | Cyclomatic complexity, method length, nesting depth, duplication |
| **Commit Hygiene** | 15 | Commit frequency, message quality, huge commits (>500 lines) |
| **Documentation** | 10 | README completeness, inline comments ratio |
| **Dependency Management** | 10 | Outdated packages, security vulnerabilities |
| **Error Handling** | 10 | Try/catch usage, null checks, defensive coding |
| **Lint & Formatting** | 5 | `dotnet format` compliance, style consistency |

### Developer Readiness Tiers

- 🔴 **Junior** (0–50): Needs foundational improvements
- 🟡 **Mid** (51–75): Good baseline, room for refinement
- 🟢 **Senior** (76–90): Production-ready with best practices
- 🌟 **Elite** (91–100): Exemplary code quality

---

## Output Examples

### JSON Output (`codegauge-report.json`)

```json
{
  "repo_name": "MyProject",
  "primary_language": "C#",
  "total_score": 72,
  "category_scores": {
    "tests": 18,
    "code_quality": 20,
    "commit_hygiene": 12,
    "documentation": 8,
    "dependencies": 9,
    "error_handling": 8,
    "lint_formatting": 4
  },
  "feedback": [
    {
      "metric": "tests",
      "score": 18,
      "issue": "Coverage below target: 68.5%.",
      "recommendation": "Increase test coverage; prioritize critical modules and error paths."
    }
  ],
  "summary_badge": {
    "score": 72,
    "color": "yellow"
  }
}
```

### Markdown Output (`codegauge-report.md`)

```markdown
# CodeGauge Report
**Repository:** MyProject  
**Primary Language:** C#  
**Total Score:** 72 (YELLOW)

## Category Scores
- Tests & Coverage: 18/25
- Code Quality: 20/25
- Commit Hygiene: 12/15
...

## Itemized Feedback
- [tests] Coverage below target: 68.5%
  - Recommendation: Increase test coverage; prioritize critical modules...
```

---

## What Each Analyzer Does

### 1. **Tests Analyzer**
- Detects test projects (xUnit, NUnit, MSTest)
- Runs `dotnet test` with XPlat Code Coverage (30s timeout)
- Parses coverage % from output (heuristic)
- Detects skipped tests (`[Fact(Skip=...)]`)

### 2. **Code Quality Analyzer** (Roslyn-powered)
- Calculates cyclomatic complexity per method
- Flags methods >50 lines
- Detects nesting depth >3 levels
- Heuristic duplication detection (5-line block matching)

### 3. **Commit Hygiene Analyzer** (LibGit2Sharp)
- Analyzes last 12 weeks of commits
- Scores commit frequency (5+/week = max points)
- Detects weak messages (<8 chars)
- Flags huge commits (>500 lines changed)

### 4. **Documentation Analyzer**
- Checks for README presence
- Validates sections: Install, Usage, Contribute, License
- Measures inline comment ratio (<3% = penalty)

### 5. **Dependency Analyzer**
- Parses `*.csproj` for `<PackageReference>`
- Queries NuGet API for latest versions
- Recommends updates for outdated packages

### 6. **Error Handling Analyzer**
- Counts `try/catch` blocks
- Detects null checks (`==`, `!=` null)
- Penalties for zero error handling in large codebases

### 7. **Lint Analyzer**
- Runs `dotnet format --verify-no-changes` (20s timeout)
- Fallback heuristics: tabs, line length >160 chars

---

## CI/CD Integration

### GitHub Actions

Create `.github/workflows/codegauge.yml`:

```yaml
name: CodeGauge Analysis

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  analyze:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Full history for commit analysis
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Clone CodeGauge
        run: git clone https://github.com/yourorg/CodeGauge.git tools/CodeGauge
      
      - name: Build CodeGauge
        run: dotnet build tools/CodeGauge/CodeGauge/CodeGauge.sln
      
      - name: Run Analysis
        run: |
          dotnet run --project tools/CodeGauge/CodeGauge/src/CodeGauge.CLI \
            -- --path . --out-json codegauge.json --out-md codegauge.md
      
      - name: Upload Reports
        uses: actions/upload-artifact@v4
        with:
          name: codegauge-reports
          path: |
            codegauge.json
            codegauge.md
      
      - name: Comment PR
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const report = fs.readFileSync('codegauge.md', 'utf8');
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: report
            });
```

---

## Architecture

```
CodeGauge/
├── src/
│   ├── CodeGauge.CLI/         # Console entry point
│   ├── CodeGauge.Core/         # Core engine, analyzers, models
│   │   ├── Analyzers/          # Base analyzers (tests, commits, docs, deps, lint)
│   │   ├── Engine/             # AnalysisEngine orchestrator
│   │   ├── Models.cs           # ScoreResult, FeedbackItem, Weights
│   │   └── RepoContext.cs      # Repository discovery
│   ├── CodeGauge.CSharp/       # C#-specific analyzers (quality, error handling)
│   └── CodeGauge.Report/       # JSON and Markdown renderers
└── CodeGauge.sln
```

### Extending to New Languages

1. Create `CodeGauge.<Language>` project
2. Implement language-specific `IAnalyzer` classes
3. Register analyzers in CLI `Program.cs`
4. Adjust weights in `Models.cs` if needed

---

## Limitations & Notes

⚠️ **Test Coverage**: Parses XPlat Code Coverage output heuristically; may not detect all formats  
⚠️ **Network Required**: Dependency analyzer queries NuGet API (failures tolerated)  
⚠️ **Timeouts**: Test runs limited to 30s, lint to 20s to prevent CI hangs  
⚠️ **Git Required**: Commit analyzer needs a Git repository; scores lower without one  

---

## Roadmap

- [ ] Python/JavaScript/Java analyzers
- [ ] Security vulnerability scanning (NVD, Snyk)
- [ ] Historical trend tracking (store scores per commit)
- [ ] Web dashboard for visualization
- [ ] VS Code extension integration
- [ ] Gamification badges (Gold Test Trophy, Clean Code Champion)

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-analyzer`)
3. Commit your changes with descriptive messages
4. Push and open a Pull Request

Run CodeGauge on your own changes to ensure quality! 🎯

---

## License

MIT License - See [LICENSE](LICENSE) for details.

---

## Support

📖 **Documentation**: See [`usage`](usage) file for detailed API reference  
🐛 **Issues**: Report bugs via GitHub Issues  
💬 **Discussions**: Share ideas and feedback in Discussions

---

**Made with ❤️ for developers who care about code quality**
