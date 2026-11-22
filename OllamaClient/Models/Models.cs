using System.Text.Json.Serialization;
namespace OllamaClient.Models;
public class GenerateResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; }
    [JsonPropertyName("response")]
    public string Response { get; set; }
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }
    [JsonPropertyName("prompt_eval_duration")]
    public long PromptEvalDuration { get; set; }
    [JsonPropertyName("eval_duration")]
    public long EvalDuration { get; set; }
    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}
public class ChatMessage
{
    public string Role { get; set; }  // "system", "user", "assistant"
    public string Content { get; set; }
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}
public class ChatRequest
{
    public string Model { get; set; }
    public List<ChatMessage> Messages { get; set; }
    public bool Stream { get; set; }
    public ChatOptions Options { get; set; }
}
public class ChatOptions
{
    public double Temperature { get; set; } = 0.7;
    public int? NumPredict { get; set; }
    public int? NumCtx { get; set; }
}
