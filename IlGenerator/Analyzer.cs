using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace IlGenerator;

// ==================== ЗАДАНИЕ 3: анализатор кода ====================
// Сравниваем динамически сгенерированные методы (задания 2.1 и 2.2)
// с эквивалентными обычными методами на C#.

public static class Analyzer
{
    public static void Run()
    {
        Console.WriteLine("=== ЗАДАНИЕ 3: анализатор кода ===\n");

        // Сначала быстрый предварительный замер на Stopwatch,
        // затем точный замер средствами BenchmarkDotNet.
        QuickStopwatchAnalysis();

        Console.WriteLine("\n--- Точный анализ (BenchmarkDotNet) ---\n");
        BenchmarkRunner.Run<FactorialBenchmark>();   // задание 3.1
        BenchmarkRunner.Run<MaxOfThreeBenchmark>();  // задание 3.2
    }

    // Предварительный (упрощённый) анализ на Stopwatch
    private static void QuickStopwatchAnalysis()
    {
        const int iterations = 1_000_000;

        var dynamicFactorial = ILGeneratorDemo.CreateRecursiveFactorialMethod();
        var dynamicMax = ILGeneratorDemo.CreateMaxOfThreeMethod();

        Console.WriteLine("--- Предварительный анализ (Stopwatch) ---\n");

        Measure("3.1 Факториал, обычный метод C#", iterations, i => Program.FactorialReference(10));
        Measure("3.1 Факториал, динамический IL ", iterations, i => dynamicFactorial(10));
        Measure("3.2 Максимум, обычный метод C# ", iterations, i => Program.MaxOfThreeReference(i, 17, i % 100));
        Measure("3.2 Максимум, динамический IL  ", iterations, i => dynamicMax(i, 17, i % 100));
    }

    private static void Measure(string title, int iterations, Func<int, int> action)
    {
        // Прогрев: заставляем JIT скомпилировать метод до начала измерений
        for (int i = 0; i < 1000; i++)
            action(i);

        var sw = Stopwatch.StartNew();
        int sink = 0;
        for (int i = 0; i < iterations; i++)
            sink += action(i);
        sw.Stop();

        double nsPerCall = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;
        Console.WriteLine($"{title}: {sw.ElapsedMilliseconds,5} мс   " +
                          $"({nsPerCall,7:F2} нс на вызов)   [контрольная сумма: {sink}]");
    }
}

// ---------- Задание 3.1: анализатор для рекурсивного факториала ----------
[ShortRunJob]
[MemoryDiagnoser]
public class FactorialBenchmark
{
    private static readonly Func<int, int> DynamicFactorial =
        ILGeneratorDemo.CreateRecursiveFactorialMethod();

    private static readonly Func<int, int> DynamicFactorialLoop =
        ILGeneratorDemo.CreateFactorialMethod();

    [Params(5, 10)]
    public int N;

    [Benchmark(Baseline = true, Description = "Обычный метод C# (рекурсия)")]
    public int StaticRecursive() => Program.FactorialReference(N);

    [Benchmark(Description = "Динамический IL (рекурсия)")]
    public int DynamicRecursive() => DynamicFactorial(N);

    [Benchmark(Description = "Динамический IL (цикл)")]
    public int DynamicLoop() => DynamicFactorialLoop(N);
}

// ---------- Задание 3.2: анализатор для максимума из трёх ----------
// Замер идёт не по одному вызову, а по массиву из 1000 наборов чисел.
// Это принципиально: если подавать на вход одни и те же константы,
// JIT вычисляет ответ ещё на этапе компиляции и замер показывает 0 нс.
[ShortRunJob]
[MemoryDiagnoser]
public class MaxOfThreeBenchmark
{
    private static readonly Func<int, int, int, int> DynamicMax =
        ILGeneratorDemo.CreateMaxOfThreeMethod();

    private const int Count = 1000;

    private int[] _a = null!, _b = null!, _c = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);   // фиксированное зерно — данные одинаковы для всех методов
        _a = new int[Count];
        _b = new int[Count];
        _c = new int[Count];
        for (int i = 0; i < Count; i++)
        {
            _a[i] = random.Next(-1000, 1000);
            _b[i] = random.Next(-1000, 1000);
            _c[i] = random.Next(-1000, 1000);
        }
    }

    [Benchmark(Baseline = true, Description = "Обычный метод C#")]
    public int StaticMax()
    {
        int acc = 0;
        for (int i = 0; i < Count; i++)
            acc += Program.MaxOfThreeReference(_a[i], _b[i], _c[i]);
        return acc;
    }

    [Benchmark(Description = "Динамический IL")]
    public int DynamicMaxOfThree()
    {
        int acc = 0;
        for (int i = 0; i < Count; i++)
            acc += DynamicMax(_a[i], _b[i], _c[i]);
        return acc;
    }

    [Benchmark(Description = "Библиотечный Math.Max")]
    public int MathMax()
    {
        int acc = 0;
        for (int i = 0; i < Count; i++)
            acc += Math.Max(_a[i], Math.Max(_b[i], _c[i]));
        return acc;
    }
}
