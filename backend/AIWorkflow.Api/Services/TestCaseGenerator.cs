using System.Text.Json;
using AIWorkflow.Api.Models;

namespace AIWorkflow.Api.Services;

public sealed class TestCaseGenerator
{
    private readonly AiService _ai;
    public TestCaseGenerator(AiService ai) => _ai = ai;

    public async Task<GeneratedTestResult> Generate(RepositoryInfo repo, TestPredictionResult prediction)
    {
        var selected = prediction.Predictions.Take(20).ToList();
        var evidence = string.Join("\n\n", repo.Evidence.Select(e => $"FILE: {e.Path}\n{e.Content}"));
        var prompt = $$"""
        Generate executable test files for these predicted tests.
        Framework: {{prediction.Framework}}
        Language: {{prediction.Language}}
        Repository: {{repo.Owner}}/{{repo.Name}}
        Predictions: {{JsonSerializer.Serialize(selected)}}
        Relevant repository evidence:
        {{evidence[..Math.Min(evidence.Length, 45000)]}}
        Return JSON only:
        {"summary":"...","tests":[{"path":"tests/...","framework":"...","language":"...","testCaseId":"...","code":"..."}]}
        Rules: use the detected framework; never invent credentials; use environment variables/placeholders for secrets; do not delete or overwrite existing tests; prefer stable selectors; no arbitrary sleeps; code must be syntactically valid.
        """;
        var response = await _ai.AskJson("You are a senior SDET. Generate safe, maintainable automated tests. Return strict JSON only.", prompt);
        if (!string.IsNullOrWhiteSpace(response))
        {
            try { return JsonSerializer.Deserialize<GeneratedTestResult>(AiService.CleanJson(response), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Local(repo, selected); }
            catch { }
        }
        return Local(repo, selected);
    }

    private static GeneratedTestResult Local(RepositoryInfo repo, List<TestCasePrediction> predictions)
    {
        var result = new GeneratedTestResult { Summary = "Fallback templates generated. Configure OpenAI for repository-specific executable test code." };
        var framework = predictions.Count == 0 ? "Unknown" : (repo.HasPlaywright ? "Playwright" : repo.HasSelenium ? "Selenium" : repo.HasDotNet ? "xUnit/NUnit" : "Generic");
        foreach (var p in predictions.Take(5))
        {
            var safe = string.Concat(p.Title.Where(char.IsLetterOrDigit)).ToLowerInvariant();
            if (repo.HasPlaywright)
            {
                result.Tests.Add(new GeneratedTest
                {
                    Path = $"tests/ai/{safe}.spec.ts",
                    Framework = "Playwright",
                    Language = "TypeScript",
                    TestCaseId = p.Id,
                    Code = $"import {{ test, expect }} from '@playwright/test';\n\ntest('{p.Title}', async ({{ page }}) => {{\n  // TODO: Replace route/locators with repository-specific evidence.\n  await page.goto(process.env.BASE_URL ?? 'http://localhost:3000');\n  await expect(page).toHaveTitle(/./);\n}});\n"
                });
            }
            else if (repo.HasDotNet)
            {
                result.Tests.Add(new GeneratedTest
                {
                    Path = $"tests/AI/{safe}Tests.cs",
                    Framework = framework,
                    Language = "C#",
                    TestCaseId = p.Id,
                    Code = $"using Xunit;\n\npublic class {safe}Tests\n{{\n    [Fact]\n    public void {safe}_is_expected()\n    {{\n        // TODO: Implement using repository-specific API/domain behavior.\n        Assert.True(true);\n    }}\n}}\n"
                });
            }
        }
        return result;
    }
}
