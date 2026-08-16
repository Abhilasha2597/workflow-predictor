using Microsoft.AspNetCore.Mvc;

namespace AIWorkflow.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", service = "AI Test & Workflow Copilot", timeUtc = DateTime.UtcNow });
}
