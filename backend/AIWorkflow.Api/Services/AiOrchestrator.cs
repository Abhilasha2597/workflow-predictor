using AIWorkflow.Api.Models;

namespace AIWorkflow.Api.Services;

public sealed class AiOrchestrator
{
    private readonly RepositoryScanner _scanner;
    private readonly TestCasePredictor _tests;
    private readonly TestCaseGenerator _generator;
    private readonly WorkflowPredictor _workflow;
    private readonly WorkflowGenerator _workflowGenerator;
    private readonly CopilotInstructionsGenerator _copilot;
    private readonly LogService _logs;

    public AiOrchestrator(RepositoryScanner scanner, TestCasePredictor tests, TestCaseGenerator generator, WorkflowPredictor workflow, WorkflowGenerator workflowGenerator, CopilotInstructionsGenerator copilot, LogService logs)
    { _scanner = scanner; _tests = tests; _generator = generator; _workflow = workflow; _workflowGenerator = workflowGenerator; _copilot = copilot; _logs = logs; }

    public async Task<object> AnalyzeAndGenerate(string owner, string repository, string? token, GenerateRequest request)
    {
        var id = _logs.Start("analyze-and-generate");
        _logs.Write(id, $"Repository {owner}/{repository}; authentication={(string.IsNullOrWhiteSpace(token) ? "anonymous/configured" : "request token")}");
        var repo = await _scanner.Scan(owner, repository, token);
        _logs.Write(id, $"Scanned {repo.FileCount} files; visibility={repo.Visibility}; tests={string.Join(",", repo.TestFrameworks)}");
        var testPrediction = await _tests.Predict(repo);
        _logs.Write(id, $"Predicted {testPrediction.Predictions.Count} test scenarios; confidence={testPrediction.OverallConfidence:P0}");
        var generated = request.GenerateTests ? await _generator.Generate(repo, testPrediction) : new GeneratedTestResult();
        var plan = request.GenerateWorkflow ? await _workflow.Predict(repo) : new WorkflowPlan();
        var yaml = request.GenerateWorkflow ? _workflowGenerator.Generate(repo, plan) : "";
        var copilot = request.GenerateCopilotInstructions ? _copilot.Generate(repo, plan, testPrediction) : "";
        var profileUrl = _logs.SaveJson(id, "repository-profile.json", repo);
        var predictionUrl = _logs.SaveJson(id, "predicted-tests.json", testPrediction);
        var generatedUrl = _logs.SaveJson(id, "generated-tests.json", generated);
        var workflowUrl = _logs.SaveText(id, "ai-generated-ci.yml", yaml);
        var copilotUrl = _logs.SaveText(id, "copilot-instructions.md", copilot);
        _logs.Write(id, "Artifacts generated successfully.");
        return new { executionId = id, repository = repo, testPrediction, generatedTests = generated, workflowPlan = plan, workflow = yaml, copilotInstructions = copilot, artifacts = new { profileUrl, predictionUrl, generatedUrl, workflowUrl, copilotUrl, logUrl = $"/api/ai/artifacts/{id}/execution.log" } };
    }
}
