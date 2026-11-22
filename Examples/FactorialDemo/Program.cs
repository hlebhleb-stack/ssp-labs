using System.Text;
using System.Text.Json;
namespace OllamaClient;
// ====== OllamaApiClient ======
public class OllamaApiClient
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly string _baseUrl;
    public OllamaApiClient(string baseUrl = "http://localhost:11434") => _baseUrl = baseUrl;
    public async Task<string> GenerateAsync(string model, string prompt, string systemPrompt = null)
    {
        var request = new { model, prompt, system = systemPrompt, stream = false, options = new { temperature = 0.7 } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("response").GetString();
    }
    public async Task<List<string>> ListModelsAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("models").EnumerateArray()
            .Select(m => m.GetProperty("name").GetString()).ToList();
    }
}
// ====== Agent ======
public abstract class Agent
{
    protected readonly OllamaApiClient _client;
    protected readonly string _model;
    protected readonly string _systemPrompt;
    public Agent(OllamaApiClient client, string model, string systemPrompt = null)
    {
        _client = client;
        _model = model;
        _systemPrompt = systemPrompt;
    }
    public abstract Task<string> ProcessAsync(string input);
}
// ====== Factorial Agent ======
public class FactorialAgent : Agent
{
    public FactorialAgent(OllamaApiClient client, string model)
        : base(client, model, "You are a math assistant specialized in calculating factorials.") { }
    public override async Task<string> ProcessAsync(string input)
    {
        var prompt = $"""
        Calculate the factorial of {input}. 
        Show the step-by-step calculation and the final result.
        If the number is large, explain the result in scientific notation.
        """;
        return await _client.GenerateAsync(_model, prompt, _systemPrompt);
    }
}
// ====== Main Program ======
class Program
{
    static async Task Main()
    {
        try
        {
            var client = new OllamaApiClient();
            var models = await client.ListModelsAsync();
            if (!models.Any())
            {
                Console.WriteLine("❌ No models found. Install: ollama pull llama2");
                return;
            }
            var model = models.First();
            Console.WriteLine($"📦 Using model: {model}\n");
            Console.WriteLine("🔢 Factorial Calculator");
            Console.WriteLine("======================\n");
            var factorialAgent = new FactorialAgent(client, model);
            Console.Write("Enter a number to calculate factorial: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out int n) || n < 0)
            {
                Console.WriteLine("❌ Please enter a valid positive integer.");
                return;
            }
            Console.WriteLine($"\n🧮 Calculating {n}! ...\n");
            Console.WriteLine("🤖 Result:\n");
            var result = await factorialAgent.ProcessAsync(n.ToString());
            Console.WriteLine(result);
            Console.WriteLine("\n✅ Done!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine("\nMake sure Ollama is running: ollama serve");
        }
    }
}
