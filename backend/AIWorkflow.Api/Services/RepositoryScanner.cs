using AIWorkflow.Api.Models;

namespace AIWorkflow.Api.Services;

public sealed class RepositoryScanner
{
    private readonly GitHubService _githubService;
    private readonly IConfiguration _config;

    public RepositoryScanner(
        GitHubService githubService,
        IConfiguration config)
    {
        _githubService = githubService;
        _config = config;
    }

    public async Task<RepositoryInfo> Scan(
        string owner,
        string repository,
        string? token = null,
        int maxFiles = 1000)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException(
                "Owner cannot be empty.",
                nameof(owner));

        if (string.IsNullOrWhiteSpace(repository))
            throw new ArgumentException(
                "Repository cannot be empty.",
                nameof(repository));

        maxFiles = Math.Clamp(
            maxFiles > 0
                ? maxFiles
                : _config.GetValue<int>(
                    "Scanning:MaxFiles",
                    1000),
            1,
            5000);

        /*
         * =====================================================
         * PUBLIC REPOSITORY PATH
         *
         * Download repository ZIP directly from
         * codeload.github.com.
         *
         * This avoids the GitHub REST API rate limit.
         * =====================================================
         */

        string repositoryPath;

        try
        {
            Console.WriteLine(
                $"Downloading public repository: " +
                $"{owner}/{repository}");

            repositoryPath =
                await _githubService.DownloadPublicRepository(
                    owner,
                    repository);

            Console.WriteLine(
                $"Repository downloaded to: {repositoryPath}");
        }
        catch
        {
            /*
             * If ZIP download fails and a token was supplied,
             * fall back to GitHub API.
             *
             * This allows private repositories to continue
             * working.
             */

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    $"Unable to download public repository " +
                    $"{owner}/{repository}. " +
                    "If this is a private repository, " +
                    "a GitHub token is required.");
            }

            return await ScanUsingGitHubApi(
                owner,
                repository,
                token,
                maxFiles);
        }

        /*
         * =====================================================
         * READ LOCAL FILES
         * =====================================================
         */

        var allFiles =
            GetLocalRepositoryFiles(
                repositoryPath,
                maxFiles);

        var files = allFiles
            .Where(IsRelevantFile)
            .ToList();

        var filePaths = files
            .Select(x => x.Path)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        /*
         * =====================================================
         * DETECTION
         * =====================================================
         */

        var testFrameworks =
            DetectTestFrameworks(files);

        var languages =
            DetectLanguages(files);

        var frameworks =
            DetectFrameworks(files);

        var infrastructure =
            DetectInfrastructure(files);

        var testFileNames = files
            .Where(IsTestFile)
            .Select(x => x.Path)
            .ToList();

        /*
         * =====================================================
         * LOCAL EVIDENCE
         *
         * IMPORTANT:
         * This does NOT call GitHub API.
         * =====================================================
         */

        var evidence =
            await CollectLocalEvidence(
                repositoryPath,
                files);

        /*
         * =====================================================
         * REPOSITORY FLAGS
         * =====================================================
         */

        var hasDocker = files.Any(x =>
            x.Name.Equals(
                "Dockerfile",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                "docker-compose.yml",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                "docker-compose.yaml",
                StringComparison.OrdinalIgnoreCase));

        var hasExistingWorkflow = files.Any(x =>
            x.Path.StartsWith(
                ".github/workflows/",
                StringComparison.OrdinalIgnoreCase));

        var hasPlaywright =
            testFrameworks.Contains(
                "Playwright",
                StringComparer.OrdinalIgnoreCase) ||
            files.Any(x =>
                x.Path.Contains(
                    "playwright",
                    StringComparison.OrdinalIgnoreCase));

        var hasSelenium =
            testFrameworks.Contains(
                "Selenium",
                StringComparer.OrdinalIgnoreCase) ||
            files.Any(x =>
                x.Path.Contains(
                    "selenium",
                    StringComparison.OrdinalIgnoreCase));

        var hasDotNet = files.Any(x =>
            x.Path.EndsWith(
                ".cs",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                ".csproj",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                ".sln",
                StringComparison.OrdinalIgnoreCase));

        var hasNode = files.Any(x =>
            x.Name.Equals(
                "package.json",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                ".js",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                ".jsx",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                ".ts",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                ".tsx",
                StringComparison.OrdinalIgnoreCase));

        var hasPython = files.Any(x =>
            x.Path.EndsWith(
                ".py",
                StringComparison.OrdinalIgnoreCase));

        var hasJava = files.Any(x =>
            x.Path.EndsWith(
                ".java",
                StringComparison.OrdinalIgnoreCase));

        /*
         * =====================================================
         * RETURN PROFILE
         * =====================================================
         */

        return new RepositoryInfo
        {
            Owner = owner,

            Name = repository,

            /*
             * ZIP download does not require repository metadata.
             * Main/master is only a fallback display value.
             */
            DefaultBranch = "main",

            RepositoryUrl =
                $"https://github.com/{owner}/{repository}",

            Visibility = "Public",

            FileCount = files.Count,

            Files = filePaths,

            Evidence = evidence,

            Languages = languages,

            Frameworks = frameworks,

            TestFrameworks = testFrameworks,

            Infrastructure = infrastructure,

            HasDocker = hasDocker,

            HasExistingWorkflow =
                hasExistingWorkflow,

            HasPlaywright =
                hasPlaywright,

            HasSelenium =
                hasSelenium,

            HasDotNet =
                hasDotNet,

            HasNode =
                hasNode,

            HasPython =
                hasPython,

            HasJava =
                hasJava,

            Summary = BuildSummary(
                owner,
                repository,
                files.Count,
                languages,
                frameworks,
                testFrameworks,
                infrastructure)
        };
    }

    // =========================================================
    // PRIVATE REPOSITORY / API FALLBACK
    // =========================================================

    private async Task<RepositoryInfo> ScanUsingGitHubApi(
        string owner,
        string repository,
        string token,
        int maxFiles)
    {
        Console.WriteLine(
            $"Using GitHub API fallback for " +
            $"{owner}/{repository}");

        var repo =
            await _githubService.GetRepository(
                owner,
                repository,
                token);

        var allFiles =
            await _githubService.GetRepositoryFilesRecursive(
                owner,
                repository,
                token,
                maxFiles);

        var files = allFiles
            .Where(IsRelevantFile)
            .ToList();

        var filePaths = files
            .Select(x => x.Path)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var testFrameworks =
            DetectTestFrameworks(files);

        var languages =
            DetectLanguages(files);

        var frameworks =
            DetectFrameworks(files);

        var infrastructure =
            DetectInfrastructure(files);

        var evidence =
            await CollectEvidence(
                owner,
                repository,
                files,
                token);

        var hasDocker = files.Any(x =>
            x.Name.Equals(
                "Dockerfile",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                "docker-compose.yml",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(
                "docker-compose.yaml",
                StringComparison.OrdinalIgnoreCase));

        var hasExistingWorkflow = files.Any(x =>
            x.Path.StartsWith(
                ".github/workflows/",
                StringComparison.OrdinalIgnoreCase));

        var hasPlaywright =
            testFrameworks.Contains(
                "Playwright",
                StringComparer.OrdinalIgnoreCase) ||
            files.Any(x =>
                x.Path.Contains(
                    "playwright",
                    StringComparison.OrdinalIgnoreCase));

        var hasSelenium =
            testFrameworks.Contains(
                "Selenium",
                StringComparer.OrdinalIgnoreCase) ||
            files.Any(x =>
                x.Path.Contains(
                    "selenium",
                    StringComparison.OrdinalIgnoreCase));

        var hasDotNet = files.Any(x =>
            x.Path.EndsWith(".cs",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(".csproj",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(".sln",
                StringComparison.OrdinalIgnoreCase));

        var hasNode = files.Any(x =>
            x.Name.Equals(
                "package.json",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(".js",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(".jsx",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(".ts",
                StringComparison.OrdinalIgnoreCase) ||
            x.Path.EndsWith(".tsx",
                StringComparison.OrdinalIgnoreCase));

        var hasPython = files.Any(x =>
            x.Path.EndsWith(
                ".py",
                StringComparison.OrdinalIgnoreCase));

        var hasJava = files.Any(x =>
            x.Path.EndsWith(
                ".java",
                StringComparison.OrdinalIgnoreCase));

        return new RepositoryInfo
        {
            Owner = owner,
            Name = repository,

            DefaultBranch =
                string.IsNullOrWhiteSpace(repo.DefaultBranch)
                    ? "main"
                    : repo.DefaultBranch,

            RepositoryUrl =
                repo.HtmlUrl ??
                $"https://github.com/{owner}/{repository}",

            Visibility =
                repo.Private ? "Private" : "Public",

            FileCount = files.Count,

            Files = filePaths,

            Evidence = evidence,

            Languages = languages,

            Frameworks = frameworks,

            TestFrameworks = testFrameworks,

            Infrastructure = infrastructure,

            HasDocker = hasDocker,

            HasExistingWorkflow =
                hasExistingWorkflow,

            HasPlaywright =
                hasPlaywright,

            HasSelenium =
                hasSelenium,

            HasDotNet =
                hasDotNet,

            HasNode =
                hasNode,

            HasPython =
                hasPython,

            HasJava =
                hasJava,

            Summary = BuildSummary(
                owner,
                repository,
                files.Count,
                languages,
                frameworks,
                testFrameworks,
                infrastructure)
        };
    }

    // =========================================================
    // LOCAL FILE SCANNER
    // =========================================================

    private static List<RepositoryFile>
        GetLocalRepositoryFiles(
            string repositoryPath,
            int maxFiles)
    {
        var result =
            new List<RepositoryFile>();

        if (!Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException(
                $"Repository directory not found: " +
                repositoryPath);

        var files = Directory
            .EnumerateFiles(
                repositoryPath,
                "*",
                SearchOption.AllDirectories);

        foreach (var fullPath in files)
        {
            if (result.Count >= maxFiles)
                break;

            var relativePath =
                Path.GetRelativePath(
                    repositoryPath,
                    fullPath)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/');

            if (string.IsNullOrWhiteSpace(relativePath))
                continue;

            var info =
                new FileInfo(fullPath);

            result.Add(
                new RepositoryFile
                {
                    Path = relativePath,
                    Name = Path.GetFileName(fullPath),
                    Sha = string.Empty,
                    Size = info.Length > int.MaxValue
                        ? int.MaxValue
                        : (int)info.Length
                });
        }

        Console.WriteLine(
            $"Local repository scan: " +
            $"{result.Count} files.");

        return result;
    }

    // =========================================================
    // LOCAL EVIDENCE
    // =========================================================

    private async Task<List<RepositoryFileEvidence>>
        CollectLocalEvidence(
            string repositoryPath,
            List<RepositoryFile> files)
    {
        var maxAiFiles =
            Math.Clamp(
                _config.GetValue<int>(
                    "Scanning:MaxAiFiles",
                    30),
                1,
                100);

        var maxBytes =
            Math.Clamp(
                _config.GetValue<int>(
                    "Scanning:MaxFileBytesForAi",
                    12000),
                1000,
                50000);

        var candidates = files
            .OrderByDescending(
                IsHighValueEvidence)
            .ThenBy(
                x => x.Path.Length)
            .Take(maxAiFiles)
            .ToList();

        var evidence =
            new List<RepositoryFileEvidence>();

        foreach (var file in candidates)
        {
            try
            {
                var fullPath =
                    Path.Combine(
                        repositoryPath,
                        file.Path.Replace(
                            '/',
                            Path.DirectorySeparatorChar));

                if (!File.Exists(fullPath))
                    continue;

                var content =
                    await File.ReadAllTextAsync(
                        fullPath);

                if (string.IsNullOrWhiteSpace(content))
                    continue;

                if (content.Length > maxBytes)
                    content = content[..maxBytes];

                evidence.Add(
                    new RepositoryFileEvidence
                    {
                        Path = file.Path,
                        Type = "file",
                        Content = content
                    });
            }
            catch
            {
                // Ignore individual unreadable files.
            }
        }

        return evidence;
    }

    // =========================================================
    // API EVIDENCE FALLBACK
    // =========================================================

    private async Task<List<RepositoryFileEvidence>>
        CollectEvidence(
            string owner,
            string repository,
            List<RepositoryFile> files,
            string? token)
    {
        var maxAiFiles =
            Math.Clamp(
                _config.GetValue<int>(
                    "Scanning:MaxAiFiles",
                    30),
                1,
                100);

        var maxBytes =
            Math.Clamp(
                _config.GetValue<int>(
                    "Scanning:MaxFileBytesForAi",
                    12000),
                1000,
                50000);

        var candidates = files
            .OrderByDescending(
                IsHighValueEvidence)
            .ThenBy(
                x => x.Path.Length)
            .Take(maxAiFiles)
            .ToList();

        var evidence =
            new List<RepositoryFileEvidence>();

        foreach (var file in candidates)
        {
            try
            {
                var content =
                    await _githubService.GetFileContent(
                        owner,
                        repository,
                        file.Path,
                        token);

                if (string.IsNullOrWhiteSpace(content))
                    continue;

                if (content.Length > maxBytes)
                    content = content[..maxBytes];

                evidence.Add(
                    new RepositoryFileEvidence
                    {
                        Path = file.Path,
                        Type = "file",
                        Content = content
                    });
            }
            catch
            {
                // Ignore individual unreadable files.
            }
        }

        return evidence;
    }

    // =========================================================
    // EVIDENCE PRIORITY
    // =========================================================

    private static int IsHighValueEvidence(
        RepositoryFile file)
    {
        var path =
            file.Path
                .Replace('\\', '/')
                .ToLowerInvariant();

        var name =
            file.Name.ToLowerInvariant();

        if (name == "package.json" ||
            name.EndsWith(
                ".csproj",
                StringComparison.OrdinalIgnoreCase))
            return 100;

        if (path.Contains("/test") ||
            path.Contains("/tests/") ||
            path.Contains("spec"))
            return 90;

        if (path.Contains(".github/workflows/"))
            return 85;

        if (name is "readme.md" or "dockerfile")
            return 80;

        if (name is
            "playwright.config.ts" or
            "playwright.config.js")
            return 80;

        if (name is
            "pytest.ini" or
            "pyproject.toml")
            return 80;

        return 10;
    }

    // =========================================================
    // RELEVANT FILE
    // =========================================================

    private static bool IsRelevantFile(
        RepositoryFile file)
    {
        if (file == null ||
            string.IsNullOrWhiteSpace(file.Path))
            return false;

        var path =
            file.Path.ToLowerInvariant();

        string[] ignoredDirectories =
        {
            "/node_modules/",
            "/bin/",
            "/obj/",
            "/dist/",
            "/build/",
            "/coverage/",
            "/.git/",
            "/packages/"
        };

        if (ignoredDirectories.Any(
                path.Contains))
            return false;

        string[] ignoredExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".ico",
            ".webp",
            ".zip",
            ".rar",
            ".7z",
            ".dll",
            ".exe",
            ".pdf",
            ".mp3",
            ".mp4",
            ".avi",
            ".mov",
            ".lock"
        };

        if (ignoredExtensions.Any(
                path.EndsWith))
            return false;

        return
            path.EndsWith(".cs") ||
            path.EndsWith(".csproj") ||
            path.EndsWith(".sln") ||
            path.EndsWith(".js") ||
            path.EndsWith(".jsx") ||
            path.EndsWith(".ts") ||
            path.EndsWith(".tsx") ||
            path.EndsWith(".java") ||
            path.EndsWith(".py") ||
            path.EndsWith(".go") ||
            path.EndsWith(".rb") ||
            path.EndsWith(".php") ||
            path.EndsWith(".c") ||
            path.EndsWith(".cpp") ||
            path.EndsWith(".h") ||
            path.EndsWith(".hpp") ||
            path.EndsWith(".json") ||
            path.EndsWith(".xml") ||
            path.EndsWith(".yaml") ||
            path.EndsWith(".yml") ||
            path.EndsWith(".feature") ||
            path.EndsWith(".md") ||
            path.EndsWith(".txt") ||
            path.EndsWith(".toml") ||
            path.EndsWith(".config") ||
            path.EndsWith(".props") ||
            path.EndsWith(".targets");
    }

    // =========================================================
    // TEST FILE
    // =========================================================

    private static bool IsTestFile(
        RepositoryFile file)
    {
        if (string.IsNullOrWhiteSpace(file.Path))
            return false;

        var path =
            file.Path.ToLowerInvariant();

        var name =
            file.Name.ToLowerInvariant();

        return
            path.Contains("/test/") ||
            path.Contains("/tests/") ||
            path.Contains("/spec/") ||
            path.Contains("/specs/") ||
            name.Contains("test") ||
            name.Contains("spec") ||
            name.EndsWith(".feature");
    }

    // =========================================================
    // LANGUAGE DETECTION
    // =========================================================

    private static List<string>
        DetectLanguages(
            List<RepositoryFile> files)
    {
        var result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var path =
                file.Path.ToLowerInvariant();

            if (path.EndsWith(".cs") ||
                path.EndsWith(".csproj") ||
                path.EndsWith(".sln"))
                result.Add("C#");

            if (path.EndsWith(".js") ||
                path.EndsWith(".jsx") ||
                path.EndsWith(".ts") ||
                path.EndsWith(".tsx"))
                result.Add("JavaScript/TypeScript");

            if (path.EndsWith(".py"))
                result.Add("Python");

            if (path.EndsWith(".java"))
                result.Add("Java");

            if (path.EndsWith(".go"))
                result.Add("Go");

            if (path.EndsWith(".rb"))
                result.Add("Ruby");

            if (path.EndsWith(".php"))
                result.Add("PHP");
        }

        return result.ToList();
    }

    // =========================================================
    // FRAMEWORK DETECTION
    // =========================================================

    private static List<string>
        DetectFrameworks(
            List<RepositoryFile> files)
    {
        var result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var paths =
            files
                .Select(x =>
                    x.Path.ToLowerInvariant())
                .ToList();

        if (paths.Any(x =>
                x.Contains("playwright")))
            result.Add("Playwright");

        if (paths.Any(x =>
                x.Contains("selenium")))
            result.Add("Selenium");

        if (paths.Any(x =>
                x.Contains("cypress")))
            result.Add("Cypress");

        if (paths.Any(x =>
                x.Contains("react")))
            result.Add("React");

        if (paths.Any(x =>
                x.Contains("angular")))
            result.Add("Angular");

        if (paths.Any(x =>
                x.EndsWith(".csproj") ||
                x.EndsWith(".sln")))
            result.Add(".NET");

        if (paths.Any(x =>
                x.EndsWith("package.json")))
            result.Add("Node.js");

        if (paths.Any(x =>
                x.EndsWith("pom.xml") ||
                x.Contains("junit")))
            result.Add("Java/JUnit");

        if (paths.Any(x =>
                x.EndsWith("pyproject.toml") ||
                x.EndsWith("pytest.ini")))
            result.Add("Python");

        return result.ToList();
    }

    // =========================================================
    // TEST FRAMEWORK DETECTION
    // =========================================================

    private static List<string>
        DetectTestFrameworks(
            List<RepositoryFile> files)
    {
        var frameworks =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var path =
                file.Path.ToLowerInvariant();

            var name =
                file.Name.ToLowerInvariant();

            if (path.Contains("playwright"))
                frameworks.Add("Playwright");

            if (path.Contains("cypress"))
                frameworks.Add("Cypress");

            if (path.Contains("jest"))
                frameworks.Add("Jest");

            if (path.Contains("mocha"))
                frameworks.Add("Mocha");

            if (path.Contains("xunit"))
                frameworks.Add("xUnit");

            if (path.Contains("nunit"))
                frameworks.Add("NUnit");

            if (path.Contains("mstest"))
                frameworks.Add("MSTest");

            if (path.Contains("junit"))
                frameworks.Add("JUnit");

            if (path.Contains("testng"))
                frameworks.Add("TestNG");

            if (path.Contains("pytest") ||
                name == "pytest.ini")
                frameworks.Add("PyTest");

            if (path.Contains("unittest"))
                frameworks.Add("Python unittest");

            if (path.EndsWith(".feature"))
                frameworks.Add("Cucumber/BDD");
        }

        return frameworks.ToList();
    }

    // =========================================================
    // INFRASTRUCTURE
    // =========================================================

    private static List<string>
        DetectInfrastructure(
            List<RepositoryFile> files)
    {
        var result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var path =
                file.Path.ToLowerInvariant();

            var name =
                file.Name.ToLowerInvariant();

            if (path.StartsWith(
                    ".github/workflows/"))
                result.Add("GitHub Actions");

            if (name == "dockerfile" ||
                path.Contains("docker-compose"))
                result.Add("Docker");

            if (name == "package.json")
                result.Add("npm");

            if (name == "pom.xml" ||
                name == "build.gradle")
                result.Add("Maven/Gradle");
        }

        return result.ToList();
    }

    // =========================================================
    // SUMMARY
    // =========================================================

    private static string BuildSummary(
        string owner,
        string repository,
        int fileCount,
        List<string> languages,
        List<string> frameworks,
        List<string> testFrameworks,
        List<string> infrastructure)
    {
        return
            $"{owner}/{repository}: " +
            $"{fileCount} relevant files; " +
            $"languages=" +
            $"{string.Join(", ",
                languages.DefaultIfEmpty("Unknown"))}; " +
            $"frameworks=" +
            $"{string.Join(", ",
                frameworks.DefaultIfEmpty("Unknown"))}; " +
            $"testing=" +
            $"{string.Join(", ",
                testFrameworks.DefaultIfEmpty(
                    "Not detected"))}; " +
            $"infrastructure=" +
            $"{string.Join(", ",
                infrastructure.DefaultIfEmpty(
                    "Not detected"))}.";
    }
}