using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIWorkflow.Api.Services;

public sealed class AiService
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public AiService(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<string?> AskJson(string system, string user)
    {
        var key = _config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return null;
        var http = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var body = new
        {
            model = _config["OpenAI:Model"] ?? "gpt-4.1-mini",
            messages = new object[] { new { role = "system", content = system }, new { role = "user", content = user } },
            temperature = 0.1
        };
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }

    public static string CleanJson(string text)
    {
        var s = text.Trim();
        if (s.StartsWith("```"))
        {
            var first = s.IndexOf('\n');
            var last = s.LastIndexOf("```", StringComparison.Ordinal);
            if (first >= 0 && last > first) s = s[(first + 1)..last];
        }
        return s.Trim();
    }
}
