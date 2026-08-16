AI Test Workflow Copilot - fixed runtime wiring

IMPORTANT FIXES
1. Program.cs registers every service used by AIController/AiOrchestrator:
   GitHubService, RepositoryScanner, AiService, TestCasePredictor,
   TestCaseGenerator, WorkflowPredictor, WorkflowGenerator,
   CopilotInstructionsGenerator, FailureAnalyzer, LogService, AiOrchestrator.
2. AIController supports both:
   POST /api/ai/analyze
   POST /api/workflow/analyze (backward-compatible alias)
3. RepositoryScanner now fills RepositoryInfo fields needed by AI prediction:
   owner, name, URL, branch, visibility, files, evidence, languages,
   frameworks, test frameworks, infrastructure and technology flags.
4. API exceptions are returned as JSON instead of unhandled exceptions.
5. Public repositories work without a GitHub token, but GitHub still rate-limits
   unauthenticated API requests. Set GitHub:Token for higher limits.
6. Private repositories require a GitHub token.
7. OpenAI is optional. If OpenAI:ApiKey is empty, deterministic local fallback
   generation/prediction is used.

BUILD
cd backend\AIWorkflow.Api
dotnet clean
dotnet restore
dotnet build
dotnet run --urls http://localhost:5080

SWAGGER
http://localhost:5080/swagger

ANALYZE
POST http://localhost:5080/api/ai/analyze
or POST http://localhost:5080/api/workflow/analyze

Example body:
{
  "repositoryUrl": "https://github.com/octocat/Hello-World",
  "owner": "",
  "repository": ""
}

FULL AI GENERATION
POST http://localhost:5080/api/ai/analyze-and-generate

Example body:
{
  "repositoryUrl": "https://github.com/octocat/Hello-World",
  "generateTests": true,
  "generateWorkflow": true,
  "generateCopilotInstructions": true
}

PRIVATE REPOSITORY
Send header:
X-GitHub-Token: <your token>

or put the token in appsettings.json under GitHub:Token.
Do not commit a real token to Git.

FRONTEND
cd frontend
npm install
npm run dev

The Vite frontend uses http://localhost:5080 by default.

FINAL DASHBOARD UPDATE
----------------------
Generated artifacts are now viewable directly inside the dashboard.
Click "View in dashboard" for repository-profile.json, predicted-tests.json,
generated-tests.json, ai-generated-ci.yml, copilot-instructions.md, and execution.log.
The artifact API returns files inline instead of forcing browser downloads.

Public repository analysis should be configured separately to avoid relying on
anonymous GitHub REST API limits; private repositories use a GitHub token.
