# AI Test & Workflow Copilot

A .NET 10 backend that accepts public or private GitHub repositories, scans repository structure and relevant source/config evidence, predicts missing/high-value test cases with AI, generates test code, predicts a GitHub Actions workflow, generates Copilot instructions, produces local artifacts/logs, publishes to GitHub, dispatches workflows and analyzes GitHub Actions logs.

## No Ollama
This project uses OpenAI when `OpenAI:ApiKey` is configured. If no API key is configured, deterministic fallback analysis/test predictions are returned. No Ollama is required.

## Public repository
```powershell
cd backend\AIWorkflow.Api
dotnet restore
dotnet build
dotnet run --urls http://localhost:5080
```
Open `http://localhost:5080/swagger`.

Example:
```json
POST /api/ai/analyze-and-generate
{
  "repositoryUrl":"https://github.com/octocat/Hello-World",
  "generateTests":true,
  "generateWorkflow":true,
  "generateCopilotInstructions":true
}
```

## Private repository
For development, create a GitHub fine-grained PAT with only the repository permissions needed by your use case. Send it as the `X-GitHub-Token` request header. Do not put it in the JSON body and do not commit it.

PowerShell:
```powershell
$headers = @{ "X-GitHub-Token" = "github_pat_..." }
Invoke-RestMethod -Uri "http://localhost:5080/api/ai/analyze-and-generate" -Method Post -Headers $headers -ContentType "application/json" -Body '{"repositoryUrl":"https://github.com/company/private-repo","generateTests":true,"generateWorkflow":true,"generateCopilotInstructions":true}'
```

You can also configure a server-side token for local development:
```powershell
$env:GitHub__Token="github_pat_..."
```
For enterprise production, replace PAT authentication with a GitHub App installation-token service; the repository scanner is already isolated behind `GitHubService` so that authentication mechanism can be swapped without changing the AI layer.

## AI
```powershell
$env:OpenAI__ApiKey="YOUR_KEY"
$env:OpenAI__Model="gpt-4.1-mini"
```
The AI receives a bounded set of relevant repository evidence rather than every binary/dependency file. Credentials are never included by the application intentionally.

## Main endpoints
- `POST /api/ai/analyze` — repository intelligence
- `POST /api/ai/predict-tests` — AI/deterministic test-gap prediction
- `POST /api/ai/generate-tests` — generated test files
- `POST /api/ai/analyze-and-generate` — one-call complete analysis
- `POST /api/github/publish` — publish workflow + Copilot instructions
- `POST /api/ai/dispatch` — dispatch GitHub Actions workflow
- `GET /api/ai/runs/{owner}/{repository}` — recent Actions runs
- `POST /api/ai/failure-from-run` — download a run's logs and AI-analyze the failure
- `POST /api/ai/analyze-failure` — analyze supplied logs
- `GET /api/health` — health check

## Logs and artifacts
Each one-call analysis creates:
`backend/AIWorkflow.Api/Artifacts/<executionId>/`

including repository-profile.json, predicted-tests.json, generated-tests.json, ai-generated-ci.yml, copilot-instructions.md and execution.log.

## Important safety behavior
The backend does not execute arbitrary source code from a remote repository. Generated tests are returned as artifacts. CI execution happens through GitHub Actions after you explicitly publish/dispatch the workflow.

## Frontend is optional
Run only the backend + Swagger if desired. To use the UI:
```powershell
cd frontend
npm.cmd install
npm.cmd run dev
```
Set `VITE_API_URL` if the backend is not at `http://localhost:5080`.
