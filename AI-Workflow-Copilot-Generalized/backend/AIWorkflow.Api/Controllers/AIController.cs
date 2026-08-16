using AIWorkflow.Api.Models;
using AIWorkflow.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Octokit;

namespace AIWorkflow.Api.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AIController : ControllerBase
{
    private readonly AiOrchestrator _orchestrator;
    private readonly RepositoryScanner _scanner;
    private readonly TestCasePredictor _predictor;
    private readonly TestCaseGenerator _generator;
    private readonly WorkflowPredictor _workflowPredictor;
    private readonly WorkflowGenerator _workflowGenerator;
    private readonly CopilotInstructionsGenerator _copilot;
    private readonly FailureAnalyzer _failure;
    private readonly GitHubService _github;
    private readonly LogService _logs;

    public AIController(
        AiOrchestrator orchestrator,
        RepositoryScanner scanner,
        TestCasePredictor predictor,
        TestCaseGenerator generator,
        WorkflowPredictor workflowPredictor,
        WorkflowGenerator workflowGenerator,
        CopilotInstructionsGenerator copilot,
        FailureAnalyzer failure,
        GitHubService github,
        LogService logs)
    {
        _orchestrator = orchestrator;
        _scanner = scanner;
        _predictor = predictor;
        _generator = generator;
        _workflowPredictor = workflowPredictor;
        _workflowGenerator = workflowGenerator;
        _copilot = copilot;
        _failure = failure;
        _github = github;
        _logs = logs;
    }

    [HttpPost("analyze-and-generate")]
    public async Task<IActionResult> AnalyzeAndGenerate([FromBody] GenerateRequest request)
    {
        try
        {
            var (owner, repo) = Parse(request);
            return Ok(await _orchestrator.AnalyzeAndGenerate(owner, repo, Token(), request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ApiException ex)
        {
            return StatusCode((int)ex.StatusCode, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("analyze")]
    [HttpPost("/api/workflow/analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        try
        {
            var (owner, repo) = Parse(request);
            var profile = await _scanner.Scan(owner, repo, Token());
            return Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ApiException ex)
        {
            return StatusCode((int)ex.StatusCode, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("predict-tests")]
    public async Task<IActionResult> PredictTests([FromBody] AnalyzeRequest request)
    {
        try
        {
            var (owner, repo) = Parse(request);
            var profile = await _scanner.Scan(owner, repo, Token());
            return Ok(await _predictor.Predict(profile));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("generate-tests")]
    public async Task<IActionResult> GenerateTests([FromBody] AnalyzeRequest request)
    {
        try
        {
            var (owner, repo) = Parse(request);
            var profile = await _scanner.Scan(owner, repo, Token());
            var prediction = await _predictor.Predict(profile);
            return Ok(await _generator.Generate(profile, prediction));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("analyze-failure")]
    public async Task<IActionResult> AnalyzeFailure([FromBody] FailureRequest request)
    {
        try
        {
            return Ok(await _failure.Analyze(request.Logs));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpGet("runs/{owner}/{repository}")]
    public async Task<IActionResult> Runs(string owner, string repository)
    {
        try
        {
            return Ok(await _github.GetWorkflowRuns(owner, repository, Token()));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("dispatch")]
    public async Task<IActionResult> Dispatch([FromBody] DispatchRequest request)
    {
        try
        {
            var token = RequiredToken();
            var dispatchId = await _github.DispatchWorkflow(
                request.Owner,
                request.Repository,
                request.WorkflowFile,
                request.Ref,
                token);

            return Ok(new
            {
                message = "Workflow dispatch accepted by GitHub.",
                dispatchId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("failure-from-run")]
    public async Task<IActionResult> FailureFromRun([FromBody] RunRequest request)
    {
        try
        {
            var token = RequiredToken();
            var id = _logs.Start("failure-from-run");
            var encoded = await _github.GetRunLogs(
                request.Owner,
                request.Repository,
                request.RunId,
                token);

            var base64 = encoded["ZIP_BYTES:".Length..];
            var zip = Convert.FromBase64String(base64);
            var temp = Path.Combine(Path.GetTempPath(), $"run-{request.RunId}.zip");
            await System.IO.File.WriteAllBytesAsync(temp, zip);

            var extract = Path.Combine(
                Path.GetTempPath(),
                $"run-{request.RunId}-{Guid.NewGuid():N}");

            System.IO.Compression.ZipFile.ExtractToDirectory(temp, extract);

            var logs = string.Join(
                "\n\n",
                Directory.EnumerateFiles(extract, "*", SearchOption.AllDirectories)
                    .Select(System.IO.File.ReadAllText));

            var analysis = await _failure.Analyze(logs);
            var analysisUrl = _logs.SaveJson(id, "failure-analysis.json", analysis);
            var logUrl = _logs.SaveText(
                id,
                "github-run.log",
                logs[..Math.Min(logs.Length, 200000)]);

            return Ok(new
            {
                executionId = id,
                runId = request.RunId,
                analysis,
                artifacts = new
                {
                    analysisUrl,
                    logUrl,
                    executionLog = $"/api/ai/artifacts/{id}/execution.log"
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpGet("artifacts/{executionId}/{fileName}")]
    public IActionResult Artifact(string executionId, string fileName)
    {
        var path = _logs.GetPath(executionId, fileName);
        if (!System.IO.File.Exists(path))
            return NotFound();

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var contentType = extension switch
        {
            ".json" => "application/json",
            ".yml" or ".yaml" => "text/yaml",
            ".md" => "text/markdown",
            _ => "text/plain"
        };

        // Return the artifact inline so the browser/dashboard can display it.
        // Do not pass a download file name here; that would cause a download.
        return PhysicalFile(path, contentType);
    }

   private string? Token()
 {
    if (Request.Headers.TryGetValue(
            "X-GitHub-Token",
            out var headerToken))
    {
        var token = headerToken.ToString();

        if (!string.IsNullOrWhiteSpace(token))
            return token.Trim();
    }

    var configuredToken =
        HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["GitHub:Token"];

    return string.IsNullOrWhiteSpace(configuredToken)
        ? null
        : configuredToken.Trim();
 }

    private string RequiredToken()
    {
        var token = Token();
        if (!string.IsNullOrWhiteSpace(token))
            return token;

        throw new InvalidOperationException(
            "A GitHub token is required for GitHub Actions and repository write operations. " +
            "Set X-GitHub-Token or GitHub:Token.");
    }

    private static (string Owner, string Repository) Parse(AnalyzeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RepositoryUrl))
        {
            var raw = request.RepositoryUrl.Trim();

            if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                raw = "https://github.com/" + raw.TrimStart('/');
            }

            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
                !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Only github.com repository URLs are supported.");
            }

            var parts = uri.AbsolutePath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                throw new ArgumentException(
                    "Use https://github.com/owner/repository.");

            var repo = parts[1].EndsWith(
                ".git",
                StringComparison.OrdinalIgnoreCase)
                ? parts[1][..^4]
                : parts[1];

            return (parts[0], repo);
        }

        if (string.IsNullOrWhiteSpace(request.Owner) ||
            string.IsNullOrWhiteSpace(request.Repository))
        {
            throw new ArgumentException(
                "Provide repositoryUrl or owner and repository.");
        }

        return (
            request.Owner.Trim(),
            request.Repository.Trim());
    }
}
