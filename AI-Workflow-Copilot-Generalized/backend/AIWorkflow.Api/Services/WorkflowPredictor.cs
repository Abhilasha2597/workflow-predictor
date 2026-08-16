using System.Text.Json;
using AIWorkflow.Api.Models;

namespace AIWorkflow.Api.Services;

public sealed class WorkflowPredictor
{
    private readonly AiService _ai;
    public WorkflowPredictor(AiService ai) => _ai = ai;

    public async Task<WorkflowPlan> Predict(RepositoryInfo repo)
    {
        var prompt = $$"""
        Predict a safe GitHub Actions CI workflow from this repository evidence.
        {{JsonSerializer.Serialize(repo)}}
        Return JSON only with name, runner, stages[], actions[], requiredSecrets[], recommendations[], reasoning, confidence.
        Never invent deployment secrets. Prefer validation/build/test. Preserve existing workflows unless necessary.
        """;
        var response = await _ai.AskJson("You are a senior DevOps architect. Return strict JSON only.", prompt);
        if (!string.IsNullOrWhiteSpace(response))
        {
            try { return JsonSerializer.Deserialize<WorkflowPlan>(AiService.CleanJson(response), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Local(repo); }
            catch { }
        }
        return Local(repo);
    }

    private static WorkflowPlan Local(RepositoryInfo r)
    {
        var p = new WorkflowPlan { Confidence = .75, Reasoning = "Deterministic fallback based on detected repository technologies." };
        if (r.HasDotNet) { p.Stages.AddRange(["Restore", "Build", "Test"]); p.Actions.AddRange(["dotnet restore", "dotnet build --no-restore", "dotnet test --no-build"]); }
        if (r.HasNode) { p.Stages.Add("Install Node dependencies"); p.Actions.Add("npm ci"); }
        if (r.HasPlaywright) { p.Stages.Add("Playwright tests"); p.Actions.AddRange(["npx playwright install --with-deps", "npx playwright test"]); }
        if (r.HasPython) { p.Stages.Add("Python tests"); p.Actions.Add("python -m pytest"); }
        if (r.HasJava) { p.Stages.Add("Java tests"); p.Actions.Add("mvn test or ./gradlew test"); }
        if (r.HasDocker) { p.Stages.Add("Docker validation"); p.Actions.Add("docker build -t application ."); }
        if (r.HasExistingWorkflow) p.Recommendations.Add("Review existing GitHub Actions workflows before enabling a duplicate workflow.");
        p.Recommendations.Add("Do not deploy to production automatically without approval.");
        return p;
    }
}
