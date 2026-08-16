using AIWorkflow.Api.Models;
using AIWorkflow.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIWorkflow.Api.Controllers;

[ApiController]
[Route("api/github")]
public sealed class PublishController : ControllerBase
{
    private readonly GitHubService _github;
    public PublishController(GitHubService github) => _github = github;

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishRequest request)
    {
        var token = Request.Headers.TryGetValue("X-GitHub-Token", out var value) ? value.ToString() : null;
        await _github.CreateOrUpdateFile(request.Owner, request.Repository, ".github/workflows/ai-generated-ci.yml", request.WorkflowYaml, "chore: add AI-generated CI workflow", request.Branch, token);
        await _github.CreateOrUpdateFile(request.Owner, request.Repository, ".github/copilot-instructions.md", request.CopilotInstructions, "docs: add AI test Copilot instructions", request.Branch, token);
        return Ok(new { message = "Workflow and Copilot instructions published." });
    }
}
