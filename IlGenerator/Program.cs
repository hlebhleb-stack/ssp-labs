namespace IlGenerator;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Запуск с аргументом bench запускает анализатор кода (задание 3)
        if (args.Length > 0 && args[0] == "bench")
        {
            Analyzer.Run();
            return;
        }

        Console.WriteLine("=== Динамическая генерация IL ===\n");

        // --- Примеры из методических указаний ---
        var addMethod = ILGeneratorDemo.CreateAddMethod();
        Console.WriteLine($"DynamicAdd(15, 27) = {addMethod(15, 27)}");

        var factorialMethod = ILGeneratorDemo.CreateFactorialMethod();
        Console.WriteLine($"DynamicFactorial(5) = {factorialMethod(5)}");
        Console.WriteLine($"DynamicFactorial(7) = {factorialMethod(7)}");

        var gradeMethod = ILGeneratorDemo.CreateGradeMethod();
        Console.WriteLine($"DynamicGrade(10) = {gradeMethod(10)}");
        Console.WriteLine($"DynamicGrade(-5) = {gradeMethod(-5)}");
        Console.WriteLine($"DynamicGrade(0) = {gradeMethod(0)}");

        // --- ЗАДАНИЕ 2.1: рекурсивный факториал ---
        Console.WriteLine("\n############ ЗАДАНИЕ 2.1: рекурсивный факториал ############\n");
        var recursiveFactorial = ILGeneratorDemo.CreateRecursiveFactorialMethod();

        for (int n = 0; n <= 12; n++)
        {
            int dynamicResult = recursiveFactorial(n);
            int staticResult = FactorialReference(n);
            string check = dynamicResult == staticResult ? "совпадает" : "ОШИБКА";
            Console.WriteLine($"DynamicFactorialRecursive({n,2}) = {dynamicResult,12}   " +
                              $"(эталон на C#: {staticResult,12}) — {check}");
        }

        // --- ЗАДАНИЕ 2.2: максимум из трёх чисел ---
        Console.WriteLine("\n############ ЗАДАНИЕ 2.2: максимум из трёх чисел ############\n");
        var maxOfThree = ILGeneratorDemo.CreateMaxOfThreeMethod();

        (int a, int b, int c)[] tests =
        {
            (1, 2, 3),      // максимум третий
            (10, 5, 7),     // максимум первый
            (4, 19, 6),     // максимум второй
            (-8, -3, -15),  // все отрицательные
            (5, 5, 5),      // все равны
            (7, 7, 2)       // два одинаковых максимума
        };

        foreach (var (a, b, c) in tests)
        {
            int dynamicResult = maxOfThree(a, b, c);
            int staticResult = Math.Max(a, Math.Max(b, c));
            string check = dynamicResult == staticResult ? "совпадает" : "ОШИБКА";
            Console.WriteLine($"DynamicMaxOfThree({a,4}, {b,4}, {c,4}) = {dynamicResult,4}   " +
                              $"(эталон Math.Max: {staticResult,4}) — {check}");
        }

        Console.WriteLine("\nДля запуска анализатора кода (задание 3): dotnet run -c Release bench");
    }

    // Эталонная реализация факториала на C# — для проверки правильности
    public static int FactorialReference(int n) => n <= 1 ? 1 : n * FactorialReference(n - 1);

    // Эталонная реализация максимума из трёх на C#
    public static int MaxOfThreeReference(int a, int b, int c)
    {
        int max = a;
        if (b > max) max = b;
        if (c > max) max = c;
        return max;
    }
}
