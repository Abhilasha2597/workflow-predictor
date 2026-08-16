using AIWorkflow.Api.Models;
using AIWorkflow.Api.Services;

namespace AIWorkflow.Tests;

public class WorkflowGeneratorTests
{
    [Fact]
    public void GeneratesPlaywrightWorkflow()
    {
        var repo = new RepositoryInfo
        {
            Owner = "test",
            Name = "repo",
            DefaultBranch = "main",
            HasNode = true,
            HasPlaywright = true
        };

        var yaml = new WorkflowGenerator()
            .Generate(repo, new WorkflowPlan());

        Assert.Contains("playwright install", yaml);
        Assert.Contains("playwright test", yaml);
        Assert.Contains("actions/checkout@v4", yaml);
    }
}
