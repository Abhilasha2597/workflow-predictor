using System.Text;
using System.Text.Json;

namespace AIWorkflow.Api.Services;

public sealed class LogService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LogService> _logger;

    public LogService(IWebHostEnvironment env, ILogger<LogService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public string Start(string operation)
    {
        var id = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..23];
        var dir = Path.Combine(_env.ContentRootPath, "Artifacts", id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "execution.log"), $"[{DateTime.UtcNow:O}] START {operation}{Environment.NewLine}");
        return id;
    }

    public void Write(string id, string message)
    {
        var path = Path.Combine(_env.ContentRootPath, "Artifacts", id, "execution.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        _logger.LogInformation("{Message}", message);
    }

    public string SaveText(string id, string fileName, string content)
    {
        var safe = Path.GetFileName(fileName);
        var path = Path.Combine(_env.ContentRootPath, "Artifacts", id, safe);
        File.WriteAllText(path, content ?? "", Encoding.UTF8);
        return $"/api/ai/artifacts/{id}/{safe}";
    }

    public string SaveJson(string id, string fileName, object value)
    {
        return SaveText(id, fileName, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    public string GetPath(string id, string fileName) => Path.Combine(_env.ContentRootPath, "Artifacts", id, Path.GetFileName(fileName));
}
