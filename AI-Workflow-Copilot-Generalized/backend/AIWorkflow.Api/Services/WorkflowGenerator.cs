using AIWorkflow.Api.Models;
using YamlDotNet.Serialization;

namespace AIWorkflow.Api.Services;

public sealed class WorkflowGenerator
{
    public string Generate(RepositoryInfo repo, WorkflowPlan plan)
    {
        var steps = new List<object> { new Dictionary<string, object?> { ["name"] = "Checkout", ["uses"] = "actions/checkout@v4" } };
        if (repo.HasDotNet)
        {
            steps.Add(new Dictionary<string, object?> { ["name"] = "Setup .NET", ["uses"] = "actions/setup-dotnet@v4", ["with"] = new Dictionary<string, object?> { ["dotnet-version"] = "10.x" } });
            steps.Add(new Dictionary<string, object?> { ["name"] = "Restore", ["run"] = "dotnet restore" });
            steps.Add(new Dictionary<string, object?> { ["name"] = "Build", ["run"] = "dotnet build --no-restore" });
            steps.Add(new Dictionary<string, object?> { ["name"] = "Test", ["run"] = "dotnet test --no-build" });
        }
        if (repo.HasNode || repo.HasPlaywright)
        {
            steps.Add(new Dictionary<string, object?> { ["name"] = "Setup Node", ["uses"] = "actions/setup-node@v4", ["with"] = new Dictionary<string, object?> { ["node-version"] = "20", ["cache"] = "npm" } });
            steps.Add(new Dictionary<string, object?> { ["name"] = "Install dependencies", ["run"] = "npm ci" });
        }
        if (repo.HasPlaywright)
        {
            steps.Add(new Dictionary<string, object?> { ["name"] = "Install Playwright browsers", ["run"] = "npx playwright install --with-deps" });
            steps.Add(new Dictionary<string, object?> { ["name"] = "Run Playwright tests", ["run"] = "npx playwright test" });
            steps.Add(new Dictionary<string, object?> { ["name"] = "Upload Playwright report", ["if"] = "always()", ["uses"] = "actions/upload-artifact@v4", ["with"] = new Dictionary<string, object?> { ["name"] = "playwright-report", ["path"] = "playwright-report/" } });
        }
        if (repo.HasPython) steps.Add(new Dictionary<string, object?> { ["name"] = "Run Python tests", ["run"] = "python -m pytest" });
        if (repo.HasDocker) steps.Add(new Dictionary<string, object?> { ["name"] = "Build Docker image", ["run"] = "docker build -t application ." });
        var workflow = new Dictionary<string, object?>
        {
            ["name"] = plan.Name,
            ["on"] = new Dictionary<string, object?> { ["push"] = new Dictionary<string, object?> { ["branches"] = new[] { repo.DefaultBranch } }, ["pull_request"] = null },
            ["jobs"] = new Dictionary<string, object?> { ["ci"] = new Dictionary<string, object?> { ["runs-on"] = plan.Runner, ["steps"] = steps } }
        };
        return new SerializerBuilder().Build().Serialize(workflow);
    }
}
