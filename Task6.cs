using System.Reflection;

namespace SSP_Lab3;

public static class Task6
{
    public static void Run()
    {
        Console.WriteLine("=== ЗАДАНИЕ 6: ВЫЗОВ ПРИВАТНОГО МЕТОДА ===\n");

        Type t = typeof(DynamicCalculator);
        object o = Activator.CreateInstance(t);

        Console.WriteLine("6.1 LogOperation:");
        var m1 = t.GetMethod("LogOperation", BindingFlags.NonPublic | BindingFlags.Instance);
        if (m1 != null) { m1.Invoke(o, new object[] { "Тест" }); Console.WriteLine("  OK"); }
        else Console.WriteLine("  не найден");

        Console.WriteLine("\n6.2 CalculateSquare:");
        var m2 = t.GetMethod("CalculateSquare", BindingFlags.NonPublic | BindingFlags.Instance);
        if (m2 != null) Console.WriteLine($"  (7) = {m2.Invoke(o, new object[] { 7 })}");
        else Console.WriteLine("  не найден");

        Console.WriteLine("\n6.3 Перегрузки Add и Multiply:");
        var ai = t.GetMethod("Add", new[] { typeof(int), typeof(int) });
        if (ai != null) Console.WriteLine($"  int Add(10,20) = {ai.Invoke(o, new object[] { 10, 20 })}");

        var ad = t.GetMethod("Add", new[] { typeof(double), typeof(double) });
        if (ad != null) Console.WriteLine($"  dbl Add(5.5,3.2) = {ad.Invoke(o, new object[] { 5.5, 3.2 })}");

        var mu = t.GetMethod("Multiply");
        if (mu != null) Console.WriteLine($"  Multiply(7,8) = {mu.Invoke(o, new object[] { 7, 8 })}");

        Console.WriteLine("\n6.4 Через делегат:");
        var md = t.GetMethod("Add", new[] { typeof(int), typeof(int) });
        if (md != null)
        {
            var fn = (Func<int, int, int>)Delegate.CreateDelegate(typeof(Func<int, int, int>), o, md);
            Console.WriteLine($"  fn(15,25) = {fn(15, 25)}");
        }
    }
}
