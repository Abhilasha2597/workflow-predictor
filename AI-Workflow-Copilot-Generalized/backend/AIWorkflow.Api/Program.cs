using AIWorkflow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HTTP client used by GitHubService and AiService
builder.Services.AddHttpClient();

// Same-origin deployment: frontend and API are served from one URL.
// Keep localhost origins allowed for local development.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ============================================================
// APPLICATION SERVICES
// Register EVERY service used by AIController/AiOrchestrator.
// ============================================================
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<RepositoryScanner>();

// AI provider. If OpenAI:ApiKey is empty, AiService returns null
// and the predictor/generator/workflow services use their local
// deterministic fallback implementations.
builder.Services.AddScoped<AiService>();

builder.Services.AddScoped<TestCasePredictor>();
builder.Services.AddScoped<TestCaseGenerator>();

builder.Services.AddScoped<WorkflowPredictor>();
builder.Services.AddScoped<WorkflowGenerator>();

builder.Services.AddScoped<CopilotInstructionsGenerator>();
builder.Services.AddScoped<FailureAnalyzer>();

builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<AiOrchestrator>();

var app = builder.Build();

// Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Test Workflow Copilot API v1");
    options.RoutePrefix = "swagger";
});

// CORS must run before controller endpoints.
app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

// Serve the Vite build from the same ASP.NET process.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
