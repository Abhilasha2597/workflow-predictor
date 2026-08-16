using AIWorkflow.Api.Models;

namespace AIWorkflow.Api.Services;

public sealed class CopilotInstructionsGenerator
{
    public string Generate(RepositoryInfo r, WorkflowPlan p, TestPredictionResult tests)
    {
        return $$"""
        # AI Test & Workflow Copilot Instructions

        ## Repository
        - Repository: `{{r.Owner}}/{{r.Name}}`
        - Languages: {{string.Join(", ", r.Languages)}}
        - Frameworks: {{string.Join(", ", r.Frameworks)}}
        - Testing: {{string.Join(", ", r.TestFrameworks)}}

        ## Testing rules
        - Preserve the existing test architecture.
        - Prefer the repository's detected test framework.
        - Prefer stable role/label/test-id locators for browser automation.
        - Avoid arbitrary sleeps.
        - Never hard-code credentials or tokens.
        - New features should include positive, negative and boundary tests.
        - Do not delete existing tests to make a generated test pass.

        ## CI rules
        - Use GitHub Actions for CI validation.
        - Keep build, test and security validation reproducible.
        - Do not enable automatic production deployment without review/approval.

        ## Predicted workflow stages
        {string.Join("\n", p.Stages.Select(s => "- " + s))}

        ## Predicted test gaps
        {string.Join("\n", tests.Predictions.Take(10).Select(t => $"- [{t.Priority}] {t.Title} ({t.Confidence:P0})"))}
        """;
    }
}
