using System.Text.Json;
using AIWorkflow.Api.Models;

namespace AIWorkflow.Api.Services;

public sealed class TestCasePredictor
{
    private readonly AiService _ai;
    public TestCasePredictor(AiService ai) => _ai = ai;

    public async Task<TestPredictionResult> Predict(RepositoryInfo repo)
    {
        var framework = repo.TestFrameworks.FirstOrDefault() ?? DetectFramework(repo);
        var language = repo.Languages.FirstOrDefault() ?? "Unknown";
        var existing = repo.Files.Count(IsTestPath);
        var evidence = string.Join("\n\n", repo.Evidence.Select(e => $"FILE: {e.Path}\n{e.Content}"));
        var prompt = $$"""
        Analyze this repository for test gaps. Do not invent functionality without evidence.
        Repository profile: {{JsonSerializer.Serialize(repo)}}
        Relevant source/config evidence:
        {{evidence[..Math.Min(evidence.Length, 50000)]}}
        Existing test files: {{existing}}
        Preferred test framework: {{framework}}
        Return JSON only:
        {"framework":"...","language":"...","existingTestFiles":0,"strategy":"...","overallConfidence":0.0,"predictions":[{"title":"...","type":"UI|API|Unit|Integration|E2E","priority":"High|Medium|Low","confidence":0.0,"reason":"...","target":"...","expectedResult":"..."}]}
        Generate 5-20 high-value missing test scenarios. Prioritize security, validation, critical user flows, error handling and boundary cases.
        """;
        var response = await _ai.AskJson("You are a senior QA architect and test-gap analyst. Return strict JSON only.", prompt);
        if (!string.IsNullOrWhiteSpace(response))
        {
            try { return JsonSerializer.Deserialize<TestPredictionResult>(AiService.CleanJson(response), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Local(repo); }
            catch { }
        }
        return Local(repo);
    }

    private static bool IsTestPath(string p)
    {
        var x = p.Replace('\\', '/').ToLowerInvariant();
        return x.Contains("/test") || x.Contains("/tests/") || x.Contains("spec") || x.Contains("__tests__") || x.EndsWith("test.cs") || x.EndsWith("tests.cs");
    }

    private static string DetectFramework(RepositoryInfo r) => r.HasPlaywright ? "Playwright" : r.HasSelenium ? "Selenium" : r.HasDotNet ? "xUnit/NUnit" : r.HasNode ? "Jest/Vitest" : r.HasPython ? "pytest" : "Unknown";

    private static TestPredictionResult Local(RepositoryInfo r)
    {
        var framework = DetectFramework(r);
        var language = r.Languages.FirstOrDefault() ?? "Unknown";
        var list = new List<TestCasePrediction>
        {
            new() { Title = "Valid input follows the primary success path", Type = "E2E", Priority = "High", Confidence = .72, Reason = "Every detected application should have a primary happy-path test.", Target = "Primary user flow", ExpectedResult = "Operation completes successfully." },
            new() { Title = "Invalid input is rejected with a useful validation message", Type = "UI", Priority = "High", Confidence = .70, Reason = "Validation/error handling is a high-value boundary case.", Target = "Detected input forms/endpoints", ExpectedResult = "Invalid data is rejected without an unhandled error." },
            new() { Title = "Unauthorized access is rejected", Type = "Security", Priority = "High", Confidence = .66, Reason = "Authorization boundaries should be explicitly tested.", Target = "Protected routes/endpoints", ExpectedResult = "Unauthorized access is denied." },
            new() { Title = "Dependency/service failure is handled gracefully", Type = "Integration", Priority = "Medium", Confidence = .61, Reason = "External dependency failure is a common production failure mode.", Target = "Service/API boundary", ExpectedResult = "Application returns a controlled error." },
            new() { Title = "Boundary and empty values are handled correctly", Type = "Unit", Priority = "Medium", Confidence = .64, Reason = "Boundary values frequently expose defects.", Target = "Validation/business logic", ExpectedResult = "Boundary conditions produce expected behavior." }
        };
        return new TestPredictionResult { Framework = framework, Language = language, ExistingTestFiles = r.Files.Count(IsTestPath), Predictions = list, Strategy = "Deterministic fallback based on repository signals; configure OpenAI for repository-specific predictions.", OverallConfidence = .66 };
    }
}
