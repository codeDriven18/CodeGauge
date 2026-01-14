# Example CI/CD Configurations

## GitHub Actions

### Basic Analysis on Push/PR

```yaml
# .github/workflows/codegauge.yml
name: Code Quality Analysis

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  codegauge:
    runs-on: windows-latest
    
    steps:
    - name: Checkout Repository
      uses: actions/checkout@v4
      with:
        fetch-depth: 0  # Full Git history for commit analysis
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Install CodeGauge
      run: |
        git clone https://github.com/yourorg/CodeGauge.git _codegauge
        dotnet build _codegauge/CodeGauge/CodeGauge.sln --configuration Release
    
    - name: Run CodeGauge Analysis
      run: |
        dotnet run --project _codegauge/CodeGauge/src/CodeGauge.CLI --no-build `
          -- --path . --out-json reports/score.json --out-md reports/score.md --verbose
    
    - name: Upload Reports
      uses: actions/upload-artifact@v4
      with:
        name: codegauge-reports
        path: reports/
    
    - name: Post PR Comment
      if: github.event_name == 'pull_request'
      uses: actions/github-script@v7
      with:
        script: |
          const fs = require('fs');
          const report = fs.readFileSync('reports/score.md', 'utf8');
          await github.rest.issues.createComment({
            issue_number: context.issue.number,
            owner: context.repo.owner,
            repo: context.repo.repo,
            body: `## 📊 CodeGauge Analysis\n\n${report}`
          });
```

### Fail Build on Low Score

```yaml
# .github/workflows/codegauge-gate.yml
name: Quality Gate

on:
  pull_request:
    branches: [ main ]

jobs:
  quality-gate:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v4
      with:
        fetch-depth: 0
    
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Run CodeGauge
      run: |
        git clone https://github.com/yourorg/CodeGauge.git _cg
        dotnet build _cg/CodeGauge/CodeGauge.sln -c Release
        dotnet run --project _cg/CodeGauge/src/CodeGauge.CLI --no-build `
          -- --path . --out-json score.json
    
    - name: Check Minimum Score
      shell: pwsh
      run: |
        $score = (Get-Content score.json | ConvertFrom-Json).total_score
        Write-Host "Total Score: $score"
        if ($score -lt 60) {
          Write-Error "Quality gate failed! Score $score is below minimum 60."
          exit 1
        }
```

---

## Azure Pipelines

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: 'windows-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '10.0.x'

- script: |
    git clone https://github.com/yourorg/CodeGauge.git $(Agent.TempDirectory)/CodeGauge
    dotnet build $(Agent.TempDirectory)/CodeGauge/CodeGauge/CodeGauge.sln --configuration Release
  displayName: 'Install CodeGauge'

- script: |
    dotnet run --project $(Agent.TempDirectory)/CodeGauge/CodeGauge/src/CodeGauge.CLI --no-build -- --path $(Build.SourcesDirectory) --out-json $(Build.ArtifactStagingDirectory)/codegauge.json --out-md $(Build.ArtifactStagingDirectory)/codegauge.md --verbose
  displayName: 'Run CodeGauge Analysis'

- task: PublishBuildArtifacts@1
  inputs:
    pathToPublish: '$(Build.ArtifactStagingDirectory)'
    artifactName: 'CodeGaugeReports'
  displayName: 'Publish Reports'
```

---

## GitLab CI

```yaml
# .gitlab-ci.yml
stages:
  - analyze

codegauge:
  stage: analyze
  image: mcr.microsoft.com/dotnet/sdk:10.0
  script:
    - git clone https://github.com/yourorg/CodeGauge.git /tmp/codegauge
    - dotnet build /tmp/codegauge/CodeGauge/CodeGauge.sln --configuration Release
    - dotnet run --project /tmp/codegauge/CodeGauge/src/CodeGauge.CLI --no-build -- --path $CI_PROJECT_DIR --out-json codegauge.json --out-md codegauge.md
  artifacts:
    paths:
      - codegauge.json
      - codegauge.md
    expire_in: 1 week
  only:
    - main
    - merge_requests
```

---

## Jenkins Pipeline

```groovy
// Jenkinsfile
pipeline {
    agent {
        label 'windows'
    }
    
    stages {
        stage('Setup') {
            steps {
                bat 'dotnet --version'
            }
        }
        
        stage('Install CodeGauge') {
            steps {
                bat '''
                    git clone https://github.com/yourorg/CodeGauge.git C:\\temp\\codegauge
                    dotnet build C:\\temp\\codegauge\\CodeGauge\\CodeGauge.sln --configuration Release
                '''
            }
        }
        
        stage('Analyze') {
            steps {
                bat '''
                    dotnet run --project C:\\temp\\codegauge\\CodeGauge\\src\\CodeGauge.CLI --no-build -- --path %WORKSPACE% --out-json reports\\score.json --out-md reports\\score.md --verbose
                '''
            }
        }
        
        stage('Archive') {
            steps {
                archiveArtifacts artifacts: 'reports/*', fingerprint: true
            }
        }
    }
}
```

---

## Local Pre-Commit Hook

```bash
# .git/hooks/pre-commit
#!/bin/bash

echo "Running CodeGauge pre-commit check..."

# Path to your local CodeGauge installation
CODEGAUGE_PATH="$HOME/tools/CodeGauge/CodeGauge"

# Run analysis
dotnet run --project "$CODEGAUGE_PATH/src/CodeGauge.CLI" --no-build \
  -- --path . --out-json .codegauge-temp.json

# Check score
SCORE=$(jq '.total_score' .codegauge-temp.json)
MIN_SCORE=50

if [ "$SCORE" -lt "$MIN_SCORE" ]; then
  echo "❌ Commit blocked: CodeGauge score ($SCORE) below minimum ($MIN_SCORE)"
  rm .codegauge-temp.json
  exit 1
else
  echo "✅ CodeGauge score: $SCORE"
  rm .codegauge-temp.json
  exit 0
fi
```

---

## Scheduled Analysis (Nightly Trend Tracking)

```yaml
# .github/workflows/codegauge-nightly.yml
name: Nightly Code Quality Trend

on:
  schedule:
    - cron: '0 2 * * *'  # 2 AM daily

jobs:
  trend:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v4
      with:
        fetch-depth: 0
    
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Run CodeGauge
      run: |
        git clone https://github.com/yourorg/CodeGauge.git _cg
        dotnet build _cg/CodeGauge/CodeGauge.sln -c Release
        dotnet run --project _cg/CodeGauge/src/CodeGauge.CLI --no-build `
          -- --path . --out-json trend/score-$(date +%Y%m%d).json
    
    - name: Commit Trend Data
      run: |
        git config user.name "CodeGauge Bot"
        git config user.email "bot@codegauge.dev"
        git add trend/
        git commit -m "📊 Daily CodeGauge trend: $(date +%Y-%m-%d)"
        git push
```

---

## Docker Container Analysis

```yaml
# .github/workflows/docker-analyze.yml
name: Docker Build Quality Check

on:
  push:
    paths:
      - 'Dockerfile'
      - 'src/**'

jobs:
  docker-quality:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    
    - name: Build Docker Image
      run: docker build -t myapp:test .
    
    - name: Run CodeGauge in Container
      run: |
        docker run --rm -v $(pwd):/workspace -w /workspace \
          mcr.microsoft.com/dotnet/sdk:10.0 \
          bash -c "
            git clone https://github.com/yourorg/CodeGauge.git /tmp/cg &&
            dotnet build /tmp/cg/CodeGauge/CodeGauge.sln &&
            dotnet run --project /tmp/cg/CodeGauge/src/CodeGauge.CLI --no-build \
              -- --path /workspace --out-json /workspace/docker-score.json
          "
    
    - name: Upload Report
      uses: actions/upload-artifact@v4
      with:
        name: docker-analysis
        path: docker-score.json
```
