using System.Text.Json;
using AIWorkflow.Api.Models;

namespace AIWorkflow.Api.Services;

public sealed class FailureAnalyzer
{
    private readonly AiService _ai;
    public FailureAnalyzer(AiService ai) => _ai = ai;

    public async Task<FailureAnalysis> Analyze(string logs)
    {
        logs ??= "";
        var prompt = $"Analyze this CI/test failure. Return JSON only with category, rootCause, severity, confidence, evidence[], suggestedFixes[], proposedWorkflowPatch.\nLOGS:\n{logs[..Math.Min(logs.Length, 30000)]}";
        var response = await _ai.AskJson("You are a senior SDET/DevOps failure analyst. Base conclusions only on evidence. Return strict JSON.", prompt);
        if (!string.IsNullOrWhiteSpace(response))
        {
            try { return JsonSerializer.Deserialize<FailureAnalysis>(AiService.CleanJson(response), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Local(logs); }
            catch { }
        }
        return Local(logs);
    }

    private static FailureAnalysis Local(string logs)
    {
        var x = logs.ToLowerInvariant();
        if (x.Contains("playwright") && x.Contains("executable doesn't exist"))
            return new FailureAnalysis { Category = "Playwright", RootCause = "Playwright browser binaries are not installed.", Severity = "Medium", Confidence = .96, Evidence = ["Playwright executable is missing."], SuggestedFixes = ["Run npx playwright install --with-deps before tests."], ProposedWorkflowPatch = "Add npx playwright install --with-deps before npx playwright test." };
        if (x.Contains("npm err") || x.Contains("npm error"))
            return new FailureAnalysis { Category = "Node", RootCause = "npm reported a dependency or script failure.", Severity = "Medium", Confidence = .72, Evidence = ["npm error output detected."], SuggestedFixes = ["Inspect package.json scripts and npm error details; run npm ci in CI." ] };
        if (x.Contains("dotnet") && x.Contains("error cs"))
            return new FailureAnalysis { Category = ".NET", RootCause = "The .NET compiler reported a C# compilation error.", Severity = "High", Confidence = .84, Evidence = ["CS compiler error detected."], SuggestedFixes = ["Fix the reported compiler error before running tests." ] };
        return new FailureAnalysis { Category = "Unknown", RootCause = "No deterministic failure rule matched.", Severity = "Medium", Confidence = .25, SuggestedFixes = ["Use AI analysis with the complete failing job log." ] };
    }
}
