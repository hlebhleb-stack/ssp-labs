using System.Text;

namespace OllamaClient.Agents;

public class AgentConfig
{
    public string Model { get; set; } = "codellama:7b";
    public string? SystemPrompt { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2048;
}

public class AgentContext
{
    public List<ChatMessage> Messages { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
    public ChatMessage(string role, string content) { Role = role; Content = content; }
}

public abstract class Agent
{
    protected readonly OllamaApiClient _client;
    protected readonly AgentConfig _config;
    protected readonly AgentContext _context = new();
    protected List<ChatMessage> _history => _context.Messages;

    protected Agent(OllamaApiClient client, AgentConfig config)
    {
        _client = client;
        _config = config;
        // При инициализации в историю добавляется системный промпт
        if (!string.IsNullOrEmpty(config.SystemPrompt))
            _history.Add(new ChatMessage("system", config.SystemPrompt));
    }

    public abstract Task<string> ProcessAsync(string input);

    // Формирует полный контекст разговора из истории сообщений
    protected string BuildPrompt(string input)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(_config.SystemPrompt))
        {
            sb.AppendLine($"System: {_config.SystemPrompt}");
            sb.AppendLine();
        }
        foreach (var msg in _history)
            if (msg.Role != "system") sb.AppendLine($"{msg.Role}: {msg.Content}");
        return sb.ToString();
    }
}

public class CodeAssistantAgent : Agent
{
    public CodeAssistantAgent(OllamaApiClient client, AgentConfig config) : base(client, config) { }

    public override async Task<string> ProcessAsync(string input)
    {
        _history.Add(new ChatMessage("user", input));
        var prompt = BuildPrompt(input);
        var response = await _client.GenerateAsync(
            _config.Model, prompt, _config.SystemPrompt, maxTokens: _config.MaxTokens);
        _history.Add(new ChatMessage("assistant", response));
        return response;
    }
}

public class CodeReviewAgent : Agent
{
    public CodeReviewAgent(OllamaApiClient client, AgentConfig config) : base(client, config) { }

    public override async Task<string> ProcessAsync(string input)
    {
        var prompt = $"""
        Пожалуйста, проверьте следующий код и предоставьте:
        1. Оценку качества кода
        2. Потенциальные ошибки или проблемы
        3. Предложения по производительности
        4. Проблемы безопасности
        5. Улучшения по лучшим практикам

        Код:
        {input}
        """;
        return await _client.GenerateAsync(
            _config.Model, prompt, _config.SystemPrompt, maxTokens: _config.MaxTokens);
    }
}

public class FactorialAgent : Agent
{
    public FactorialAgent(OllamaApiClient client, string model)
        : base(client, new AgentConfig { Model = model, SystemPrompt = "You are a math assistant specialized in calculating factorials." }) { }

    public override async Task<string> ProcessAsync(string input)
    {
        var prompt = $"""
        Calculate the factorial of {input}.
        Show the step-by-step calculation and the final result.
        If the number is large, explain the result in scientific notation.
        """;
        return await _client.GenerateAsync(
            _config.Model, prompt, _config.SystemPrompt, maxTokens: _config.MaxTokens);
    }
}

public class AgentPipeline
{
    private readonly List<Agent> _agents = new();
    public void AddAgent(Agent agent) => _agents.Add(agent);

    public async Task<string> ProcessAsync(string input)
    {
        var result = input;
        var step = 1;
        foreach (var agent in _agents)
        {
            Console.WriteLine($"  Агент {step}/{_agents.Count} обрабатывает...");
            result = await agent.ProcessAsync(result);
            Console.WriteLine($"  Агент {step} завершён.");
            step++;
        }
        return result;
    }
}
