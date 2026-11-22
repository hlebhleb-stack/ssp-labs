using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace JBenchMark;

public class Program
{
    private const int ArraySize = 10_000;

    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Готовим массив из 10000 целых чисел
        int[] data = new int[ArraySize];
        for (int i = 0; i < data.Length; i++)
            data[i] = i + 1;

        long expected = (long)ArraySize * (ArraySize + 1) / 2; // сумма 1..10000 = 50 005 000

        Console.WriteLine("############ ЗАДАНИЕ 4.1: влияние JIT-компиляции ############\n");
        Console.WriteLine($"Размер массива: {ArraySize} элементов (значения 1..{ArraySize})");
        Console.WriteLine($"Ожидаемая сумма: {expected}");
        Console.WriteLine($"Tiered Compilation: {(IsTieredCompilationEnabled() ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}\n");

        var sumMethod = typeof(Program).GetMethod(nameof(SumArray),
            BindingFlags.Public | BindingFlags.Static)!;

        // ---------- 1. БЕЗ JIT: интерпретация IL-кода ----------
        Console.WriteLine("--- 1. БЕЗ JIT-компиляции (интерпретация IL) ---");
        var sw = Stopwatch.StartNew();
        int interpretedResult = (int)IlInterpreter.Execute(sumMethod, data)!;
        sw.Stop();
        double interpretedMs = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Результат: {interpretedResult}  (верно: {interpretedResult == expected})");
        Console.WriteLine($"Время одного прохода: {interpretedMs:F3} мс");
        Console.WriteLine($"На один элемент: {interpretedMs * 1_000_000 / ArraySize:F1} нс\n");

        // ---------- 2. ХОЛОДНЫЙ СТАРТ: первый вызов, JIT работает прямо сейчас ----------
        Console.WriteLine("--- 2. Холодный старт (первый вызов, включает работу JIT) ---");
        sw.Restart();
        int coldResult = SumArrayCold(data);
        sw.Stop();
        double coldMs = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Результат: {coldResult}");
        Console.WriteLine($"Время первого вызова: {coldMs:F3} мс");
        Console.WriteLine("   (в это время входит компиляция метода из IL в машинный код)\n");

        // ---------- 3. С JIT: метод уже скомпилирован ----------
        Console.WriteLine("--- 3. С JIT-компиляцией (метод уже скомпилирован и прогрет) ---");

        // Прогрев: даём JIT скомпилировать метод и подняться на оптимизированный уровень
        for (int i = 0; i < 10_000; i++)
            SumArray(data);

        const int repeats = 10_000;
        sw.Restart();
        int jitResult = 0;
        for (int i = 0; i < repeats; i++)
            jitResult = SumArray(data);
        sw.Stop();
        double jitMs = sw.Elapsed.TotalMilliseconds / repeats;
        Console.WriteLine($"Результат: {jitResult}  (верно: {jitResult == expected})");
        Console.WriteLine($"Среднее время прохода ({repeats} повторов): {jitMs:F5} мс");
        Console.WriteLine($"На один элемент: {jitMs * 1_000_000 / ArraySize:F3} нс\n");

        // ---------- Итоговая таблица ----------
        Console.WriteLine("--- ИТОГ ---");
        Console.WriteLine($"{"Режим выполнения",-42}{"Время, мс",14}{"Замедление",14}");
        Console.WriteLine(new string('-', 70));
        Console.WriteLine($"{"Без JIT (интерпретация IL)",-42}{interpretedMs,14:F3}{interpretedMs / jitMs,13:F0}x");
        Console.WriteLine($"{"Холодный старт (первый вызов + JIT)",-42}{coldMs,14:F3}{coldMs / jitMs,13:F0}x");
        Console.WriteLine($"{"С JIT (прогретый метод)",-42}{jitMs,14:F5}{1.0,13:F0}x");
        Console.WriteLine();
        Console.WriteLine($"Интерпретация медленнее JIT-кода примерно в {interpretedMs / jitMs:F0} раз.");
        Console.WriteLine($"Первый (холодный) вызов медленнее прогретого примерно в {coldMs / jitMs:F0} раз.");

        Console.WriteLine("\nЧтобы повторить замер с выключенной многоуровневой компиляцией:");
        Console.WriteLine("  DOTNET_TieredCompilation=0 dotnet run -c Release");

        // ---------- Вторая часть: сравнение способов вызова ----------
        JitVisualization.ComparePerformance();
        JitVisualization.InspectMethod(sumMethod);
        JitVisualization.CompareJitModes();
    }

    /// <summary>Сложение элементов массива. Этот метод и интерпретируется, и компилируется JIT.</summary>
    public static int SumArray(int[] data)
    {
        int sum = 0;
        for (int i = 0; i < data.Length; i++)
            sum += data[i];
        return sum;
    }

    /// <summary>Точная копия SumArray. Нужна отдельная копия, чтобы замерить именно первый,
    /// ещё не скомпилированный вызов (SumArray к тому моменту уже вызывался интерпретатором).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int SumArrayCold(int[] data)
    {
        int sum = 0;
        for (int i = 0; i < data.Length; i++)
            sum += data[i];
        return sum;
    }

    private static bool IsTieredCompilationEnabled()
    {
        string? value = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation");
        return value != "0";
    }
}
