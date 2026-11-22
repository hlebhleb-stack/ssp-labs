using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace OllamaClient;
// ====== OllamaApiClient Class ======
public class OllamaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    public OllamaApiClient(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }
    /// <summary>
    /// Получить список всех установленных моделей
    /// </summary>
    public async Task<List<string>> ListModelsAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var models = document.RootElement.GetProperty("models");
        var modelNames = new List<string>();
        foreach (var model in models.EnumerateArray())
        {
            modelNames.Add(model.GetProperty("name").GetString());
        }
        return modelNames;
    }
    /// <summary>
    /// Сгенерировать ответ от модели
    /// </summary>
    public async Task<string> GenerateAsync(
        string model,
        string prompt,
        string systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model,
            prompt,
            system = systemPrompt,
            stream = false,
            options = new { temperature = 0.7 }
        };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/api/generate",
            content,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseJson);
        return document.RootElement.GetProperty("response").GetString();
    }
    /// <summary>
    /// Сгенерировать ответ с потоковой передачей
    /// </summary>
    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string model,
        string prompt,
        string systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model,
            prompt,
            system = systemPrompt,
            stream = true,
            options = new { temperature = 0.7 }
        };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(
            $"{_baseUrl}/api/generate",
            content,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            var responseText = document.RootElement.GetProperty("response").GetString();
            var done = document.RootElement.GetProperty("done").GetBoolean();
            if (!string.IsNullOrEmpty(responseText))
                yield return responseText;
            if (done) break;
        }
    }
}
// ====== Agent Classes ======
public class AgentConfig
{
    public string Model { get; set; }
    public string SystemPrompt { get; set; }
    public double Temperature { get; set; } = 0.7;
}
public class AgentContext
{
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
public class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}
public abstract class Agent
{
    protected readonly OllamaApiClient _client;
    protected readonly AgentConfig _config;
    protected readonly AgentContext _context;
    public Agent(OllamaApiClient client, AgentConfig config)
    {
        _client = client;
        _config = config;
        _context = new AgentContext();
        // Инициализация системным промптом
        if (!string.IsNullOrEmpty(config.SystemPrompt))
        {
            _context.Messages.Add(new ChatMessage("system", config.SystemPrompt));
        }
    }
    public abstract Task<string> ProcessAsync(string input);
}
public class CodeAssistantAgent : Agent
{
    private readonly string _language;
    public CodeAssistantAgent(
        OllamaApiClient client,
        AgentConfig config,
        string language = "C#")
        : base(client, config)
    {
        _language = language;
    }
    public override async Task<string> ProcessAsync(string input)
    {
        // Добавление сообщения пользователя в контекст
        _context.Messages.Add(new ChatMessage("user", input));
        // Формирование полного промпта с контекстом
        var prompt = BuildPrompt(input);
        // Генерация ответа
        var response = await _client.GenerateAsync(
            _config.Model,
            prompt,
            _config.SystemPrompt
        );
        // Добавление ответа ассистента в контекст
        _context.Messages.Add(new ChatMessage("assistant", response));
        return response;
    }
    private string BuildPrompt(string input)
    {
        var sb = new StringBuilder();
        // Добавление системного промпта
        if (!string.IsNullOrEmpty(_config.SystemPrompt))
        {
            sb.AppendLine($"System: {_config.SystemPrompt}");
            sb.AppendLine();
        }
        // Добавление истории разговора
        foreach (var msg in _context.Messages)
        {
            if (msg.Role != "system")
            {
                sb.AppendLine($"{msg.Role}: {msg.Content}");
            }
        }
        return sb.ToString();
    }
}
public class CodeReviewAgent : Agent
{
    public CodeReviewAgent(OllamaApiClient client, AgentConfig config)
        : base(client, config)
    {
    }
    public override async Task<string> ProcessAsync(string input)
    {
        var prompt = BuildReviewPrompt(input);
        return await _client.GenerateAsync(
            _config.Model,
            prompt,
            _config.SystemPrompt
        );
    }
    private string BuildReviewPrompt(string code)
    {
        return $"""
        Пожалуйста, проверьте следующий код и предоставьте:
        1. Оценку качества кода
        2. Потенциальные ошибки или проблемы
        3. Предложения по производительности
        4. Проблемы безопасности
        5. Улучшения по лучшим практикам
        Код:
        {code}
        """;
    }
}
// ====== Main Program ======
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         Ollama Intelligent Agent Demo                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        try
        {
            // Initialize the client
            var client = new OllamaApiClient();
            // Check if Ollama is running and get available models
            Console.WriteLine("🔍 Checking for available models...");
            var models = await client.ListModelsAsync();
            if (models.Count == 0)
            {
                Console.WriteLine("❌ No models found. Please install a model first.");
                Console.WriteLine("   Run: ollama pull llama2");
                Console.WriteLine("   Or:  ollama pull mistral");
                Console.WriteLine("   Or:  ollama pull codellama");
                return;
            }
            Console.WriteLine($"✅ Found {models.Count} model(s): {string.Join(", ", models)}");
            Console.WriteLine();
            // Select a model (prefer code-specific models if available)
            var model = models.FirstOrDefault(m => m.Contains("code") || m.Contains("codellama"))
                        ?? models.First();
            Console.WriteLine($"📦 Using model: {model}");
            Console.WriteLine();
            // ====== DEMO 1: Code Assistant Agent ======
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.WriteLine("📝 DEMO 1: Code Assistant Agent");
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.WriteLine();
            var codeAssistantConfig = new AgentConfig
            {
                Model = model,
                SystemPrompt = "You are an expert programming assistant. Provide clear, " +
                               "well-explained code examples with comments. Follow best " +
                               "practices and explain your reasoning."
            };
            var codeAssistant = new CodeAssistantAgent(client, codeAssistantConfig, "C#");
            // Test queries for Code Assistant
            var queries = new[]
            {
                "Write a C# method to calculate the Fibonacci sequence using recursion",
                "How do I read a CSV file in C# using LINQ?",
                "Create a simple REST API endpoint in ASP.NET Core that returns a list of users"
            };
            foreach (var query in queries)
            {
                Console.WriteLine($"👤 User: {query}");
                Console.WriteLine();
                Console.WriteLine("🤖 Assistant: ");
                Console.WriteLine();
                var response = await codeAssistant.ProcessAsync(query);
                Console.WriteLine(response);
                Console.WriteLine();
                Console.WriteLine(new string('─', 80));
                Console.WriteLine();
            }
            // ====== DEMO 2: Code Review Agent ======
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.WriteLine("🔍 DEMO 2: Code Review Agent");
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.WriteLine();
            var codeReviewConfig = new AgentConfig
            {
                Model = model,
                SystemPrompt = "You are a senior software engineer specializing in code " +
                               "reviews. Be thorough, constructive, and provide actionable " +
                               "feedback."
            };
            var codeReviewer = new CodeReviewAgent(client, codeReviewConfig);
            // Sample code to review
            var codeToReview = @"
public class Calculator
{
    public int Add(string numbers)
    {
        if (string.IsNullOrEmpty(numbers))
            return 0;
        var parts = numbers.Split(',');
        var sum = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            sum += int.Parse(parts[i]);
        }
        return sum;
    }
    public int Divide(int a, int b)
    {
        return a / b;
    }
    public List<int> GetPrimes(int limit)
    {
        var primes = new List<int>();
        for (int i = 2; i <= limit; i++)
        {
            var isPrime = true;
            for (int j = 2; j < i; j++)
            {
                if (i % j == 0)
                {
                    isPrime = false;
                    break;
                }
            }
            if (isPrime)
                primes.Add(i);
        }
        return primes;
    }
}";
            Console.WriteLine("📄 Code to Review:");
            Console.WriteLine("```csharp");
            Console.WriteLine(codeToReview);
            Console.WriteLine("```");
            Console.WriteLine();
            Console.WriteLine("🔎 Review Results:");
            Console.WriteLine();
            var reviewResult = await codeReviewer.ProcessAsync(codeToReview);
            Console.WriteLine(reviewResult);
            Console.WriteLine();
            // ====== DEMO 3: Interactive Session ======
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.WriteLine("💬 DEMO 3: Interactive Session");
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("Type 'exit' to quit the interactive session.");
            Console.WriteLine();
            var interactiveAssistant = new CodeAssistantAgent(client, codeAssistantConfig);
            while (true)
            {
                Console.Write("👤 You: ");
                var userInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
                {
                    Console.WriteLine("👋 Goodbye!");
                    break;
                }
                Console.WriteLine();
                Console.WriteLine("🤖 Assistant: ");
                Console.WriteLine();
                var response = await interactiveAssistant.ProcessAsync(userInput);
                Console.WriteLine(response);
                Console.WriteLine();
                Console.WriteLine(new string('─', 80));
                Console.WriteLine();
            }
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              Demo Completed Successfully!               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Connection refused"))
        {
            Console.WriteLine();
            Console.WriteLine("❌ ERROR: Cannot connect to Ollama server.");
            Console.WriteLine();
            Console.WriteLine("Please make sure Ollama is running:");
            Console.WriteLine("  • Windows: Run 'ollama serve' in Command Prompt");
            Console.WriteLine("  • macOS/Linux: Run 'ollama serve' in Terminal");
            Console.WriteLine("  • Or download from: https://ollama.ai/download");
            Console.WriteLine();
            Console.WriteLine("Once Ollama is running, install a model:");
            Console.WriteLine("  • ollama pull llama2");
            Console.WriteLine("  • ollama pull mistral");
            Console.WriteLine("  • ollama pull codellama");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine($"Details: {ex}");
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
