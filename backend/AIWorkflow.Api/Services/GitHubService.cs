using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Octokit;
using AIWorkflow.Api.Models;
using System.IO.Compression;
namespace AIWorkflow.Api.Services;

public sealed class GitHubService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _factory;

    public GitHubService(
        IConfiguration config,
        IHttpClientFactory factory)
    {
        _config = config;
        _factory = factory;
    }

    // =========================================================
    // TOKEN
    // =========================================================

    private string? GetToken(string? requestToken = null)
    {
        if (!string.IsNullOrWhiteSpace(requestToken))
            return requestToken.Trim();

        var configured = _config["GitHub:Token"];

        return string.IsNullOrWhiteSpace(configured)
            ? null
            : configured.Trim();
    }

    // =========================================================
    // OCTOKIT CLIENT
    // =========================================================

    private GitHubClient Client(string? requestToken = null)
    {
        var client = new GitHubClient(
            new Octokit.ProductHeaderValue(
                "AI-Test-Workflow-Copilot"));

        var token = GetToken(requestToken);

        if (!string.IsNullOrWhiteSpace(token))
        {
            client.Credentials = new Credentials(token);
        }

        return client;
    }

    // =========================================================
    // GET REPOSITORY
    // =========================================================

    public async Task<Repository> GetRepository(
        string owner,
        string name,
        string? token = null)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException(
                "Repository owner cannot be empty.",
                nameof(owner));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Repository name cannot be empty.",
                nameof(name));

        try
        {
            return await Client(token)
                .Repository
                .Get(owner, name);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException(
                    $"Repository '{owner}/{name}' was not found, " +
                    "or it is private and the supplied GitHub token " +
                    "does not have access.");
            }

            if (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(
                    await BuildRateLimitMessageAsync(
                        token,
                        "GitHub rejected the repository request."));
            }

            throw;
        }
    }

    // =========================================================
    // GET REPOSITORY FILES
    //
    // Uses Git Trees API instead of recursively calling
    // /contents for every directory.
    // =========================================================

    public async Task<IReadOnlyList<RepositoryFile>>
        GetRepositoryFilesRecursive(
            string owner,
            string repository,
            string? token = null,
            int maxFiles = 1000)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException(
                "Repository owner cannot be empty.",
                nameof(owner));

        if (string.IsNullOrWhiteSpace(repository))
            throw new ArgumentException(
                "Repository name cannot be empty.",
                nameof(repository));

        maxFiles = Math.Clamp(maxFiles, 1, 5000);

        var repo = await GetRepository(
            owner,
            repository,
            token);

        var branch = string.IsNullOrWhiteSpace(repo.DefaultBranch)
            ? "main"
            : repo.DefaultBranch;

        var client = Client(token);

        Branch branchInfo;

        try
        {
            branchInfo = await client.Repository.Branch.Get(
                owner,
                repository,
                branch);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(
                    await BuildRateLimitMessageAsync(
                        token,
                        "GitHub rejected the branch request."));
            }

            throw;
        }

        var treeSha = branchInfo.Commit.Sha;

        if (string.IsNullOrWhiteSpace(treeSha))
        {
            throw new InvalidOperationException(
                "GitHub returned an empty tree SHA.");
        }

        using var http = ApiClient(token);

        var url =
            $"/repos/{Uri.EscapeDataString(owner)}" +
            $"/{Uri.EscapeDataString(repository)}" +
            $"/git/trees/{Uri.EscapeDataString(treeSha)}" +
            "?recursive=1";

        HttpResponseMessage response;

        try
        {
            response = await http.GetAsync(url);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Unable to connect to GitHub.",
                ex);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                await BuildRateLimitMessageAsync(
                    token,
                    "GitHub repository scanning was rate-limited."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"GitHub could not find " +
                $"{owner}/{repository} " +
                $"or branch '{branch}'.");
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        var result = new List<RepositoryFile>();

        if (!root.TryGetProperty(
                "tree",
                out var treeElement))
        {
            return result;
        }

        foreach (var item in treeElement.EnumerateArray())
        {
            if (result.Count >= maxFiles)
                break;

            var type = GetString(item, "type");

            if (!string.Equals(
                    type,
                    "blob",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = GetString(item, "path");

            if (string.IsNullOrWhiteSpace(path))
                continue;

            var sha = GetString(item, "sha");

            var size = GetInt32(item, "size");

            result.Add(new RepositoryFile
            {
                Path = path,
                Name = System.IO.Path.GetFileName(path),
                Sha = sha,
                Size = size
            });
        }

        Console.WriteLine(
            $"GitHub scan: {owner}/{repository}");

        Console.WriteLine(
            $"Branch: {branch}");

        Console.WriteLine(
            $"Files found: {result.Count}");

        if (root.TryGetProperty(
                "truncated",
                out var truncated))
        {
            Console.WriteLine(
                $"Tree truncated: {truncated.GetBoolean()}");
        }

        return result;
    }

    // =========================================================
    // GET FILE CONTENT
    // =========================================================

    public async Task<string> GetFileContent(
        string owner,
        string repository,
        string path,
        string? token = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        using var http = ApiClient(token);

        var encodedPath = EncodePath(path);

        var url =
            $"/repos/{Uri.EscapeDataString(owner)}" +
            $"/{Uri.EscapeDataString(repository)}" +
            $"/contents/{encodedPath}";

        var response = await http.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return string.Empty;

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                await BuildRateLimitMessageAsync(
                    token,
                    $"GitHub rate-limited reading '{path}'."));
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        if (!root.TryGetProperty(
                "content",
                out var contentElement))
        {
            return string.Empty;
        }

        var encodedContent = contentElement.GetString();

        if (string.IsNullOrWhiteSpace(encodedContent))
            return string.Empty;

        encodedContent = encodedContent
            .Replace("\r", "")
            .Replace("\n", "");

        try
        {
            return Encoding.UTF8.GetString(
                Convert.FromBase64String(encodedContent));
        }
        catch
        {
            return encodedContent;
        }
    }

    // =========================================================
    // CREATE / UPDATE FILE
    // =========================================================

    public async Task CreateOrUpdateFile(
        string owner,
        string repository,
        string path,
        string content,
        string commitMessage,
        string branch,
        string? token = null)
    {
        var effectiveToken = GetToken(token);

        if (string.IsNullOrWhiteSpace(effectiveToken))
        {
            throw new InvalidOperationException(
                "A GitHub token is required to modify a repository.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "File path cannot be empty.",
                nameof(path));
        }

        var client = Client(effectiveToken);

        IReadOnlyList<RepositoryContent> existing;

        try
        {
            existing = await client.Repository.Content
                .GetAllContents(
                    owner,
                    repository,
                    path);
        }
        catch (ApiException ex)
            when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            existing = [];
        }

        if (existing.Count > 0)
        {
            await client.Repository.Content.UpdateFile(
                owner,
                repository,
                path,
                new UpdateFileRequest(
                    commitMessage,
                    content,
                    existing[0].Sha,
                    branch));
        }
        else
        {
            await client.Repository.Content.CreateFile(
                owner,
                repository,
                path,
                new CreateFileRequest(
                    commitMessage,
                    content,
                    branch));
        }
    }

    // =========================================================
    // WORKFLOW DISPATCH
    // =========================================================

    public async Task<long> DispatchWorkflow(
        string owner,
        string repository,
        string workflowFile,
        string branch,
        string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "A GitHub token is required to dispatch a workflow.");
        }

        using var http = ApiClient(token);

        var payload = JsonSerializer.Serialize(
            new
            {
                @ref = branch
            });

        var url =
            $"/repos/{Uri.EscapeDataString(owner)}" +
            $"/{Uri.EscapeDataString(repository)}" +
            $"/actions/workflows/" +
            $"{Uri.EscapeDataString(workflowFile)}/dispatches";

        var response = await http.PostAsync(
            url,
            new StringContent(
                payload,
                Encoding.UTF8,
                "application/json"));

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                await BuildRateLimitMessageAsync(
                    token,
                    "GitHub rejected workflow dispatch."));
        }

        response.EnsureSuccessStatusCode();

        return DateTimeOffset.UtcNow
            .ToUnixTimeMilliseconds();
    }

    // =========================================================
    // WORKFLOW RUNS
    // =========================================================

    public async Task<JsonDocument> GetWorkflowRuns(
        string owner,
        string repository,
        string? token = null,
        int count = 10)
    {
        var effectiveToken = GetToken(token);

        if (string.IsNullOrWhiteSpace(effectiveToken))
        {
            throw new InvalidOperationException(
                "A GitHub token is required to read Actions runs.");
        }

        using var http = ApiClient(effectiveToken);

        var safeCount = Math.Clamp(count, 1, 100);

        var url =
            $"/repos/{Uri.EscapeDataString(owner)}" +
            $"/{Uri.EscapeDataString(repository)}" +
            $"/actions/runs?per_page={safeCount}";

        var response = await http.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                await BuildRateLimitMessageAsync(
                    effectiveToken,
                    "GitHub rejected the Actions request."));
        }

        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
    }

    // =========================================================
    // WORKFLOW LOGS
    // =========================================================

    public async Task<string> GetRunLogs(
        string owner,
        string repository,
        long runId,
        string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "A GitHub token is required to read workflow logs.");
        }

        using var http = ApiClient(token);

        var url =
            $"/repos/{Uri.EscapeDataString(owner)}" +
            $"/{Uri.EscapeDataString(repository)}" +
            $"/actions/runs/{runId}/logs";

        var response = await http.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Workflow run '{runId}' was not found.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                await BuildRateLimitMessageAsync(
                    token,
                    "GitHub rejected the workflow log request."));
        }

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();

        return "ZIP_BYTES:" +
               Convert.ToBase64String(bytes);
    }

    // =========================================================
    // API CLIENT
    // =========================================================

    private HttpClient ApiClient(string? token)
    {
        var http = _factory.CreateClient();

        http.BaseAddress = new Uri(
            _config["GitHub:ApiBaseUrl"]
            ?? "https://api.github.com");

        http.DefaultRequestHeaders.UserAgent.Clear();

        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "AI-Test-Workflow-Copilot");

        http.DefaultRequestHeaders.Accept.Clear();

        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));

        if (!string.IsNullOrWhiteSpace(token))
        {
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);
        }

        return http;
    }

    // =========================================================
    // RATE LIMIT INFORMATION
    // =========================================================

    private async Task<string> BuildRateLimitMessageAsync(
        string? token,
        string prefix)
    {
        try
        {
            using var http = ApiClient(token);

            var response = await http.GetAsync(
                "/rate_limit");

            var remaining =
                response.Headers.TryGetValues(
                    "X-RateLimit-Remaining",
                    out var remainingValues)
                    ? remainingValues.FirstOrDefault()
                    : null;

            var reset =
                response.Headers.TryGetValues(
                    "X-RateLimit-Reset",
                    out var resetValues)
                    ? resetValues.FirstOrDefault()
                    : null;

            if (long.TryParse(reset, out var resetUnix))
            {
                var resetTime =
                    DateTimeOffset
                        .FromUnixTimeSeconds(resetUnix)
                        .ToLocalTime();

                return
                    $"{prefix} " +
                    $"Remaining GitHub API requests: " +
                    $"{remaining ?? "unknown"}. " +
                    $"Rate limit resets around " +
                    $"{resetTime:yyyy-MM-dd HH:mm:ss}. " +
                    "For reliable public-repository analysis, " +
                    "provide a GitHub token.";
            }

            return
                $"{prefix} " +
                $"Remaining GitHub API requests: " +
                $"{remaining ?? "unknown"}. " +
                "Provide a GitHub token for a higher limit.";
        }
        catch
        {
            return
                $"{prefix} " +
                "GitHub API rate limit exceeded. " +
                "Provide a GitHub token for a higher limit.";
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static string GetString(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                   property,
                   out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt32(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                   property,
                   out var value) &&
               value.TryGetInt32(out var number)
            ? number
            : 0;
    }

    private static string EncodePath(string path)
    {
        return string.Join(
            "/",
            path.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
    }
    public async Task<string> DownloadPublicRepository(
    string owner,
    string repository,
    string branch = "main")
  {
    if (string.IsNullOrWhiteSpace(owner))
        throw new ArgumentException("Owner is required.");

    if (string.IsNullOrWhiteSpace(repository))
        throw new ArgumentException("Repository is required.");

    var branches = new List<string>();

    if (!string.IsNullOrWhiteSpace(branch))
        branches.Add(branch);

    if (!branches.Contains("main"))
        branches.Add("main");

    if (!branches.Contains("master"))
        branches.Add("master");

    foreach (var currentBranch in branches)
    {
        var http = _factory.CreateClient();

        http.DefaultRequestHeaders.UserAgent.Clear();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "AI-Test-Workflow-Copilot");

        var url =
            $"https://codeload.github.com/" +
            $"{Uri.EscapeDataString(owner)}/" +
            $"{Uri.EscapeDataString(repository)}/" +
            $"zip/refs/heads/{Uri.EscapeDataString(currentBranch)}";

        try
        {
            var response = await http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                continue;

            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "AIWorkflow",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempDirectory);

            var zipPath = Path.Combine(
                tempDirectory,
                "repository.zip");

            await using (var stream =
                await response.Content.ReadAsStreamAsync())
            {
                await using var file =
                    File.Create(zipPath);

                await stream.CopyToAsync(file);
            }

            var extractPath = Path.Combine(
                tempDirectory,
                "repository");

            ZipFile.ExtractToDirectory(
                zipPath,
                extractPath);

            Console.WriteLine(
                $"Public repository downloaded: " +
                $"{owner}/{repository}");

            Console.WriteLine(
                $"Branch: {currentBranch}");

            return extractPath;
        }
        catch (HttpRequestException)
        {
            // Try next branch
        }
    }

    throw new InvalidOperationException(
        $"Unable to download public repository " +
        $"{owner}/{repository}. " +
        $"Tried branches: {string.Join(", ", branches)}.");
  }
}