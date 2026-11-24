using OllamaClient;
using OllamaClient.Agents;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var client = new OllamaApiClient();

Console.WriteLine("========================================");
Console.WriteLine("КЛИЕНТ ЛОКАЛЬНОЙ LLM OLLAMA");
Console.WriteLine("========================================\n");

try
{
    var models = await client.ListModelsAsync();
    Console.WriteLine("✓ Подключено к Ollama. Доступные модели:");
    foreach (var m in models) Console.WriteLine($"  - {m}");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine("✗ Ошибка подключения к Ollama.");
    Console.WriteLine($"  {ex.Message}");
    Console.WriteLine("  Убедитесь, что Ollama запущен на localhost:11434");
    return;
}

if (args.Length == 1 && args[0] == "--all")
{
    await RunAssignments();
    return;
}

while (true)
{
    Console.WriteLine("Выберите задание:");
    Console.WriteLine("  A - Потоковая генерация кода с системным промптом");
    Console.WriteLine("  B - Агент-помощник по коду");
    Console.WriteLine("  C - Ревью кода (потоковое)");
    Console.WriteLine("  D - Конвейер агентов");
    Console.WriteLine("  E - Свободный промпт");
    Console.WriteLine("  Примеры из методички:");
    Console.WriteLine("  1 - Малый пример: калькулятор факториала");
    Console.WriteLine("  2 - Большой пример: ассистент, ревью и диалог");
    Console.WriteLine("  3 - Тест клиента: список моделей, обычный и потоковый ответ");
    Console.WriteLine("  4 - Выполнить A–D подряд");
    Console.WriteLine("  Q - Выход");
    Console.Write("Ваш выбор: ");
    var choice = Console.ReadLine()?.Trim().ToUpperInvariant();
    Console.WriteLine();
    if (choice is null or "Q") break;

    switch (choice)
    {
        case "A": await TaskA(); break;
        case "B": await TaskB(); break;
        case "C": await TaskC(); break;
        case "D": await TaskD(); break;
        case "E": await TaskE(); break;
        case "1": await ExampleFactorial(); break;
        case "2": await ExampleBig(); break;
        case "3": await ExampleClientTest(); break;
        case "4": await RunAssignments(); break;
        default: Console.WriteLine("Неизвестный пункт.\n"); break;
    }
}

async Task RunAssignments()
{
    await TaskA();
    await TaskB();
    await TaskC();
    await TaskD();
}

async Task TaskA()
{
    Console.WriteLine("=== ЗАДАНИЕ A: Генерация кода ===\n");
    var prompt = "Напишите C# метод для поиска максимального элемента в массиве";
    Console.WriteLine($"Промпт: {prompt}\n");
    Console.WriteLine("Ответ:");
    await foreach (var token in client.GenerateStreamAsync("codellama:7b", prompt,
        "Вы — эксперт-разработчик на C#. Предоставляйте чистый, эффективный код с комментариями."))
        Console.Write(token);
    Console.WriteLine("\n");
}

async Task TaskB()
{
    Console.WriteLine("=== ЗАДАНИЕ B: Агент-помощник по коду ===\n");
    var config = new AgentConfig
    {
        Model = "codellama:7b",
        SystemPrompt = "Вы — эксперт-разработчик на C#. Предоставляйте краткий, чистый и эффективный код. " +
                       "Всегда включайте комментарии, объясняющие ваш код. " +
                       "Предлагайте лучшие практики и паттерны проектирования, где это уместно.",
        Temperature = 0.7
    };
    var assistant = new CodeAssistantAgent(client, config);
    var codePrompt = "Создайте C# класс, реализующий потокобезопасный паттерн Singleton";
    Console.WriteLine($"Пользователь: {codePrompt}\n");
    var response = await assistant.ProcessAsync(codePrompt);
    Console.WriteLine("Ассистент:");
    Console.WriteLine(response + "\n");
}

async Task TaskC()
{
    Console.WriteLine("=== ЗАДАНИЕ C: Потоковая передача с системным промптом ===\n");
    var systemPrompt = "Вы — полезный ревьювер кода. Анализируйте код на: 1. Логические ошибки " +
                       "2. Проблемы производительности 3. Нарушения стиля кода 4. Уязвимости безопасности. " +
                       "Предоставляйте конструктивную обратную связь.";
    var code = @"public int CalculateAverage(int[] numbers) {
    int sum = 0;
    for (int i = 0; i <= numbers.Length; i++) {
        sum += numbers[i];
    }
    return sum / numbers.Length;
}";
    Console.WriteLine("Код для проверки:\n" + code + "\n");
    Console.WriteLine("Ревью:");
    await foreach (var token in client.GenerateStreamAsync("deepseek-coder:6.7b", code, systemPrompt))
        Console.Write(token);
    Console.WriteLine("\n");
}

async Task TaskD()
{
    Console.WriteLine("=== ЗАДАНИЕ D: Конвейер агентов ===\n");
    var pipeline = new AgentPipeline();
    pipeline.AddAgent(new CodeAssistantAgent(client,
        new AgentConfig { Model = "codellama:7b", SystemPrompt = "Вы — эксперт по генерации кода." }));
    pipeline.AddAgent(new CodeReviewAgent(client,
        new AgentConfig { Model = "deepseek-coder:6.7b", SystemPrompt = "Вы — эксперт по ревью кода." }));
    var task = "Напишите C# метод для вычисления последовательности Фибоначчи с использованием рекурсии. " +
               "Включите правильную обработку ошибок и документацию.";
    Console.WriteLine($"Задача: {task}\n");
    var result = await pipeline.ProcessAsync(task);
    Console.WriteLine("Финальный результат:");
    Console.WriteLine(result + "\n");
}

async Task TaskE()
{
    Console.WriteLine("=== ЗАДАНИЕ E: Свободный промпт ===\n");
    Console.Write("Модель (Enter = codellama:7b): ");
    var model = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(model)) model = "codellama:7b";
    Console.Write("Системный промпт (Enter = пусто): ");
    var sys = Console.ReadLine();
    Console.Write("Ваш вопрос: ");
    var q = Console.ReadLine() ?? "";
    Console.WriteLine("\nОтвет:");
    try
    {
        await foreach (var token in client.GenerateStreamAsync(model, q, string.IsNullOrWhiteSpace(sys) ? null : sys))
            Console.Write(token);
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"✗ Ошибка запроса: {ex.Message}");
        Console.WriteLine("  Проверьте имя модели (ollama list) и что вопрос не пустой.");
    }
    Console.WriteLine("\n");
}


async Task ExampleFactorial()
{
    Console.WriteLine("=== Малый пример: калькулятор факториала ===\n");
    var models = await client.ListModelsAsync();
    var model = models.First();
    Console.WriteLine($"📦 Using model: {model}\n");
    Console.Write("Enter a number to calculate factorial: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out int n) || n < 0)
    {
        Console.WriteLine("❌ Please enter a valid positive integer.\n");
        return;
    }
    Console.WriteLine($"\n🧮 Calculating {n}! ...\n\n🤖 Result:\n");
    var agent = new FactorialAgent(client, model);
    Console.WriteLine(await agent.ProcessAsync(n.ToString()));
    Console.WriteLine("\n✅ Done!\n");
}

async Task ExampleBig()
{
    Console.WriteLine("=== Большой пример: ассистент, ревью и диалог ===\n");
    var models = await client.ListModelsAsync();
    var model = models.FirstOrDefault(m => m.Contains("code")) ?? models.First();
    Console.WriteLine($"📦 Using model: {model}\n");

    Console.WriteLine("📝 DEMO 1: Code Assistant Agent\n");
    var assistantCfg = new AgentConfig
    {
        Model = model,
        SystemPrompt = "You are an expert programming assistant. Provide clear, well-explained code examples " +
                       "with comments. Follow best practices and explain your reasoning."
    };
    var assistant = new CodeAssistantAgent(client, assistantCfg);
    var queries = new[]
    {
        "Write a C# method to calculate the Fibonacci sequence using recursion",
        "How do I read a CSV file in C# using LINQ?",
        "Create a simple REST API endpoint in ASP.NET Core that returns a list of users"
    };
    foreach (var q in queries)
    {
        Console.WriteLine($"👤 User: {q}\n\n🤖 Assistant:\n");
        Console.WriteLine(await assistant.ProcessAsync(q));
        Console.WriteLine("\n" + new string('─', 80) + "\n");
    }

    Console.WriteLine("🔍 DEMO 2: Code Review Agent\n");
    var reviewer = new CodeReviewAgent(client, new AgentConfig
    {
        Model = model,
        SystemPrompt = "You are a senior software engineer specializing in code reviews. " +
                       "Be thorough, constructive, and provide actionable feedback."
    });
    var codeToReview = @"public class Calculator
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
    Console.WriteLine("📄 Code to Review:\n" + codeToReview + "\n\n🔎 Review Results:\n");
    Console.WriteLine(await reviewer.ProcessAsync(codeToReview) + "\n");

    Console.WriteLine("💬 DEMO 3: Interactive Session\n");
    Console.WriteLine("Type 'exit' to quit the interactive session.\n");
    var interactive = new CodeAssistantAgent(client, assistantCfg);
    while (true)
    {
        Console.Write("👤 You: ");
        var userInput = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
        {
            Console.WriteLine("👋 Goodbye!\n");
            break;
        }
        Console.WriteLine("\n🤖 Assistant:\n");
        Console.WriteLine(await interactive.ProcessAsync(userInput));
        Console.WriteLine("\n" + new string('─', 80) + "\n");
    }
}

async Task ExampleClientTest()
{
    Console.WriteLine("=== Тест клиента: список моделей, обычный и потоковый ответ ===\n");
    var models = await client.ListModelsAsync();
    Console.WriteLine($"Found {models.Count} model(s):");
    foreach (var m in models) Console.WriteLine($"  - {m}");
    var selected = models.First();
    Console.WriteLine($"\nGenerating response using model: {selected}");
    Console.WriteLine("\n--- Non-streaming response ---");
    Console.WriteLine("Prompt: Explain what is a neural network in one sentence\n");
    Console.WriteLine(await client.GenerateAsync(selected, "Explain what is a neural network in one sentence"));
    Console.WriteLine("\n--- Streaming response ---");
    Console.WriteLine("Prompt: Write a haiku about programming\n");
    await foreach (var chunk in client.GenerateStreamAsync(selected, "Write a haiku about programming"))
        Console.Write(chunk);
    Console.WriteLine("\n\nDemo completed successfully!\n");
}
