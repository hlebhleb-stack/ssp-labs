using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace JBenchMark;

public static class JitVisualization
{
    /// <summary>Инспекция метода: адрес скомпилированного кода, размер IL, атрибуты.</summary>
    public static unsafe void InspectMethod(MethodInfo method)
    {
        Console.WriteLine($"\n=== Инспекция метода: {method.Name} ===\n");

        // Заставляем JIT скомпилировать метод прямо сейчас
        RuntimeHelpers.PrepareMethod(method.MethodHandle);

        // Получаем адрес скомпилированного машинного кода
        var methodHandle = method.MethodHandle;
        var functionPointer = methodHandle.GetFunctionPointer();
        Console.WriteLine($"Адрес метода: 0x{functionPointer.ToInt64():X16}");

        // Информация о теле метода
        var methodBody = method.GetMethodBody();
        if (methodBody != null)
        {
            Console.WriteLine($"Размер IL: {methodBody.GetILAsByteArray()?.Length ?? 0} байт");
            Console.WriteLine($"Локальные переменные: {methodBody.LocalVariables.Count}");
            Console.WriteLine($"Максимальный стек: {methodBody.MaxStackSize}");
        }

        Console.WriteLine($"\nАтрибуты метода:");
        Console.WriteLine($"  - IsVirtual:  {method.IsVirtual}");
        Console.WriteLine($"  - IsStatic:   {method.IsStatic}");
        Console.WriteLine($"  - IsAbstract: {method.IsAbstract}");
        Console.WriteLine($"  - IsFinal:    {method.IsFinal}");
    }

    /// <summary>Показывает текущий режим JIT-компиляции.</summary>
    public static void CompareJitModes()
    {
        Console.WriteLine("\n=== Сравнение режимов JIT ===\n");

        var config = new Dictionary<string, bool>
        {
            ["TieredCompilation"] = GetTieredCompilationSetting()
        };

        foreach (var setting in config)
            Console.WriteLine($"{setting.Key}: {(setting.Value ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");

        Console.WriteLine("\nДля изменения режимов добавьте в .csproj:");
        Console.WriteLine("<TieredCompilation>false</TieredCompilation>");
    }

    private static bool GetTieredCompilationSetting()
    {
        // Режим можно переопределить переменной окружения DOTNET_TieredCompilation
        string? fromEnvironment = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation");
        if (fromEnvironment == "0") return false;

        try
        {
            var configProperty = typeof(RuntimeHelpers).GetProperty("IsTieredCompilation");
            if (configProperty != null)
                return (bool)configProperty.GetValue(null)!;
        }
        catch { }

        return true; // По умолчанию многоуровневая компиляция включена
    }

    /// <summary>
    /// Профилирование производительности: сравнение трёх способов вызова одной и той же операции.
    /// Это законченный вариант программы из методических указаний.
    /// </summary>
    public static void ComparePerformance()
    {
        Console.WriteLine("\n=== Профилирование производительности ===\n");

        const int iterations = 10_000_000;
        var sw = new Stopwatch();

        // 1. Прямой вызов (операция встроена в цикл, вызова как такового нет)
        sw.Start();
        int sum1 = 0;
        for (int i = 0; i < iterations; i++)
            sum1 += i;
        sw.Stop();
        long directMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"Прямой вызов:          {directMs,5} мс   (сумма: {sum1})");

        // 2. Через делегат
        Func<int, int, int> add = (x, y) => x + y;
        sw.Restart();
        int sum2 = 0;
        for (int i = 0; i < iterations; i++)
            sum2 = add(sum2, i);
        sw.Stop();
        long delegateMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"Через делегат:         {delegateMs,5} мс   (сумма: {sum2})");

        // 3. Через виртуальный метод
        var calc = new Calculator();
        sw.Restart();
        int sum3 = 0;
        for (int i = 0; i < iterations; i++)
            sum3 = calc.Add(sum3, i);
        sw.Stop();
        long virtualMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"Через виртуальный метод: {virtualMs,3} мс   (сумма: {sum3})");

        // 4. Через рефлексию — самый медленный способ вызова
        var addMethod = typeof(Calculator).GetMethod(nameof(Calculator.Add))!;
        const int reflectionIterations = 1_000_000; // в 10 раз меньше: рефлексия слишком медленная
        object[] arguments = new object[2];
        sw.Restart();
        int sum4 = 0;
        for (int i = 0; i < reflectionIterations; i++)
        {
            arguments[0] = sum4;
            arguments[1] = i;
            sum4 = (int)addMethod.Invoke(calc, arguments)!;
        }
        sw.Stop();
        // Пересчитываем на то же число итераций, чтобы цифры можно было сравнивать
        long reflectionMs = sw.ElapsedMilliseconds * (iterations / reflectionIterations);
        Console.WriteLine($"Через рефлексию:       {reflectionMs,5} мс   (пересчёт с {reflectionIterations} вызовов)");

        Console.WriteLine("\n--- Во сколько раз медленнее прямого вызова ---");
        double baseline = Math.Max(directMs, 1);
        Console.WriteLine($"Делегат:            {delegateMs / baseline,6:F1}x");
        Console.WriteLine($"Виртуальный метод:  {virtualMs / baseline,6:F1}x");
        Console.WriteLine($"Рефлексия:          {reflectionMs / baseline,6:F1}x");
    }
}

public class Calculator
{
    public virtual int Add(int a, int b)
    {
        return a + b;
    }
}
