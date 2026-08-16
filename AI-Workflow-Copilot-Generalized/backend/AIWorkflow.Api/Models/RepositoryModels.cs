namespace AIWorkflow.Api.Models;

public class AnalyzeRequest
{
    public string RepositoryUrl { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
}

public sealed class GenerateRequest : AnalyzeRequest
{
    public bool GenerateTests { get; set; } = true;
    public bool GenerateWorkflow { get; set; } = true;
    public bool GenerateCopilotInstructions { get; set; } = true;
}

public sealed class PublishRequest
{
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Branch { get; set; } = "main";
    public string WorkflowYaml { get; set; } = "";
    public string CopilotInstructions { get; set; } = "";
}

public sealed class DispatchRequest
{
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
    public string WorkflowFile { get; set; } = ".github/workflows/ai-generated-ci.yml";
    public string Ref { get; set; } = "main";
}

public sealed class FailureRequest
{
    public string Logs { get; set; } = "";
}

public sealed class RunRequest
{
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
    public long RunId { get; set; }
}

public sealed class RepositoryInfo
{
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
    public string RepositoryUrl { get; set; } = "";
    public string Visibility { get; set; } = "unknown";
    public int FileCount { get; set; }
    public List<string> Files { get; set; } = [];
    public List<RepositoryFileEvidence> Evidence { get; set; } = [];
    public List<string> Languages { get; set; } = [];
    public List<string> Frameworks { get; set; } = [];
    public List<string> TestFrameworks { get; set; } = [];
    public List<string> Infrastructure { get; set; } = [];
    public bool HasDocker { get; set; }
    public bool HasExistingWorkflow { get; set; }
    public bool HasPlaywright { get; set; }
    public bool HasSelenium { get; set; }
    public bool HasDotNet { get; set; }
    public bool HasNode { get; set; }
    public bool HasPython { get; set; }
    public bool HasJava { get; set; }
    public string Summary { get; set; } = "";
}

public sealed class RepositoryFileEvidence
{
    public string Path { get; set; } = "";
    public string Type { get; set; } = "file";
    public string Content { get; set; } = "";
}

public sealed class TestCasePrediction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Type { get; set; } = "UI";
    public string Priority { get; set; } = "Medium";
    public double Confidence { get; set; }
    public string Reason { get; set; } = "";
    public string Target { get; set; } = "";
    public string ExpectedResult { get; set; } = "";
}

public sealed class TestPredictionResult
{
    public string Framework { get; set; } = "Unknown";
    public string Language { get; set; } = "Unknown";
    public int ExistingTestFiles { get; set; }
    public List<TestCasePrediction> Predictions { get; set; } = [];
    public string Strategy { get; set; } = "";
    public double OverallConfidence { get; set; }
}

public sealed class GeneratedTest
{
    public string Path { get; set; } = "";
    public string Framework { get; set; } = "";
    public string Language { get; set; } = "";
    public string TestCaseId { get; set; } = "";
    public string Code { get; set; } = "";
}

public sealed class GeneratedTestResult
{
    public List<GeneratedTest> Tests { get; set; } = [];
    public string Summary { get; set; } = "";
}

public sealed class WorkflowPlan
{
    public string Name { get; set; } = "AI Generated CI";
    public string Runner { get; set; } = "ubuntu-latest";
    public List<string> Stages { get; set; } = [];
    public List<string> Actions { get; set; } = [];
    public List<string> RequiredSecrets { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public string Reasoning { get; set; } = "";
    public double Confidence { get; set; }
}

public sealed class FailureAnalysis
{
    public string Category { get; set; } = "Unknown";
    public string RootCause { get; set; } = "";
    public string Severity { get; set; } = "Medium";
    public double Confidence { get; set; }
    public List<string> Evidence { get; set; } = [];
    public List<string> SuggestedFixes { get; set; } = [];
    public string ProposedWorkflowPatch { get; set; } = "";
}
