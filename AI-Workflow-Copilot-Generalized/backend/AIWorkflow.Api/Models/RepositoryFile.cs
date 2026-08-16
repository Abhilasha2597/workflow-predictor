namespace AIWorkflow.Api.Models;

public sealed class RepositoryFile
{
    public string Path { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Sha { get; set; } = string.Empty;

    public int Size { get; set; }
}