using System.Reflection;

namespace SSP_Lab3;

[AttributeUsage(AttributeTargets.Class)]
public class PluginAttribute : Attribute
{
    public string Name { get; }
    public PluginAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Method)]
public class PluginMethodAttribute : Attribute
{
    public string Description { get; }
    public PluginMethodAttribute(string description) => Description = description;
}

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Execute();
}

public interface IParameterizedPlugin : IPlugin
{
    void ExecuteWithParams(Dictionary<string, object> parameters);
}

[Plugin("MySimplePlugin")]
public class MyPlugin : IPlugin
{
    public string Name => "MySimplePlugin";
    public string Version => "1.0.0";

    [PluginMethod("Основной метод")]
    public void Execute()
    {
        Console.WriteLine("Привет из плагина!");
    }

    [PluginMethod("Метод для сложения")]
    public int Add(int a, int b)
    {
        return a + b;
    }
}

[Plugin("Calculator")]
public class CalculatorPlugin : IPlugin
{
    public string Name => "Calculator";
    public string Version => "1.0.0";

    public void Execute()
    {
        Console.WriteLine("Calculator plugin loaded");
    }

    public double Add(double a, double b) => a + b;
    public double Subtract(double a, double b) => a - b;
}

[Plugin("Logger")]
public class LoggerPlugin : IParameterizedPlugin
{
    public string Name => "Logger";
    public string Version => "1.0.0";

    public void Execute()
    {
        Console.WriteLine("Logger plugin executed");
    }

    public void ExecuteWithParams(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("message", out var msg))
        {
            Console.WriteLine($"[LOG] {msg}");
        }
    }
}

public class PluginManager
{
    private readonly List<IPlugin> _plugins = new List<IPlugin>();
    private readonly string _configPath;

    public PluginManager(string configPath = "plugins.txt")
    {
        _configPath = configPath;
    }

    public void LoadPluginsFromConfig()
    {
        if (!File.Exists(_configPath))
        {
            Console.WriteLine($"  Конфигурационный файл не найден: {_configPath}");
            return;
        }

        string[] pluginNames = File.ReadAllLines(_configPath);
        Assembly assembly = Assembly.GetExecutingAssembly();

        foreach (string pluginName in pluginNames)
        {
            if (string.IsNullOrWhiteSpace(pluginName)) continue;

            Type[] types = assembly.GetTypes();
            foreach (var type in types)
            {
                PluginAttribute attr = type.GetCustomAttribute<PluginAttribute>();
                if (attr != null && attr.Name == pluginName.Trim())
                {
                    if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        IPlugin plugin = (IPlugin)Activator.CreateInstance(type);
                        _plugins.Add(plugin);
                        Console.WriteLine($"  Loaded plugin: {plugin.Name} v{plugin.Version}");
                    }
                }
            }
        }
    }

    public IEnumerable<IPlugin> GetPlugins() => _plugins;

    public IPlugin GetPlugin(string name)
    {
        return _plugins.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void ExecutePluginMethod(IPlugin plugin, string methodName, object[] parameters)
    {
        Type pluginType = plugin.GetType();
        MethodInfo method = pluginType.GetMethod(methodName);

        if (method != null)
        {
            var result = method.Invoke(plugin, parameters);
            Console.WriteLine($"  Method {methodName} executed. Result: {result ?? "null"}");
        }
        else
        {
            Console.WriteLine($"  Method {methodName} not found in plugin {plugin.Name}");
        }
    }
        public T ExecutePluginMethod<T>(IPlugin plugin, string methodName, object[] parameters)
    {
        Type pluginType = plugin.GetType();
        MethodInfo method = pluginType.GetMethod(methodName);

        if (method != null && method.ReturnType == typeof(T))
        {
            return (T)method.Invoke(plugin, parameters);
        }
        return default;
    }
}

public static class Task7
{
    public static void Run()
    {
        Console.WriteLine("=== ЗАДАНИЕ 7: СИСТЕМА ПЛАГИНОВ ===\n");

        Console.WriteLine("7.1 Демонстрация атрибутов и рефлексии (MyPlugin):");
        IPlugin myPlugin = new MyPlugin();
        Type type = myPlugin.GetType();

        PluginAttribute classAttr = type.GetCustomAttribute<PluginAttribute>();
        Console.WriteLine($"  Имя плагина: {classAttr?.Name ?? "Не указано"}");

        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Console.WriteLine("\n  Методы плагина:");
        foreach (MethodInfo method in methods)
        {
            if (method.DeclaringType == typeof(object)) continue;

            PluginMethodAttribute methodAttr = method.GetCustomAttribute<PluginMethodAttribute>();
            Console.WriteLine($"\n  Метод: {method.Name}");
            if (methodAttr != null)
                Console.WriteLine($"    Описание: {methodAttr.Description}");

            try
            {
                if (method.Name == "Execute")
                    method.Invoke(myPlugin, null);
                else if (method.Name == "Add")
                {
                    object result = method.Invoke(myPlugin, new object[] { 5, 3 });
                    Console.WriteLine($"    Результат: {result}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Ошибка: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        Console.WriteLine("\n  Динамический вызов по имени:");
        CallMethodByName(myPlugin, "Add", 10, 20);
        CallMethodByName(myPlugin, "Execute");

        Console.WriteLine("\n7.2 Загрузка плагинов из конфигурационного файла:");
        string configPath = "plugins.txt";
        File.WriteAllLines(configPath, new[] { "Calculator", "Logger" });
        var manager = new PluginManager(configPath);
        manager.LoadPluginsFromConfig();

        Console.WriteLine("\n7.3 Выполнение плагинов:");
        foreach (var plugin in manager.GetPlugins())
        {
            Console.WriteLine($"\n  Плагин: {plugin.Name} v{plugin.Version}");
            plugin.Execute();

            PluginAttribute attr = plugin.GetType().GetCustomAttribute<PluginAttribute>();
            if (attr != null)
                Console.WriteLine($"  Атрибут [Plugin]: {attr.Name}");
        }

        Console.WriteLine("\n7.4 Вызов метода через рефлексию (CalculatorPlugin):");
        var calcPlugin = manager.GetPlugin("Calculator");
        if (calcPlugin != null)
        {
            manager.ExecutePluginMethod(calcPlugin, "Add", new object[] { 10.0, 5.0 });
            manager.ExecutePluginMethod(calcPlugin, "Subtract", new object[] { 10.0, 3.0 });

            double result = manager.ExecutePluginMethod<double>(calcPlugin, "Add", new object[] { 7.0, 3.0 });
            Console.WriteLine($"  Результат через generic метод Add(7, 3): {result}");
        }

        Console.WriteLine("\n7.5 Вызов метода с параметрами (LoggerPlugin):");
        var loggerPlugin = manager.GetPlugin("Logger");
        if (loggerPlugin is IParameterizedPlugin paramPlugin)
        {
            paramPlugin.ExecuteWithParams(new Dictionary<string, object>
            {
                { "message", "Выполнено успешно" }
            });
        }
    }
        static void CallMethodByName(IPlugin plugin, string methodName, params object[] parameters)
    {
        Type type = plugin.GetType();
        MethodInfo method = type.GetMethod(methodName);
        if (method != null)
        {
            Console.WriteLine($"\n  Вызов {methodName}()");
            object result = method.Invoke(plugin, parameters);
            if (result != null)
                Console.WriteLine($"    Результат: {result}");
        }
        else
        {
            Console.WriteLine($"\n  Метод {methodName} не найден");
        }
    }
}