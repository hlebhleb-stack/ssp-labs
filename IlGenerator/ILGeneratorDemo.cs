using System.Reflection.Emit;

namespace IlGenerator;

public static class ILGeneratorDemo
{
    // ==================== ПРИМЕРЫ ИЗ МЕТОДИЧЕСКИХ УКАЗАНИЙ ====================

    // Сложение двух чисел: int Add(int a, int b) => a + b;
    public static Func<int, int, int> CreateAddMethod()
    {
        // Создаём динамический метод
        DynamicMethod dynamicMethod = new DynamicMethod(
            "DynamicAdd",
            typeof(int),                        // Возвращаемый тип
            new[] { typeof(int), typeof(int) }, // входные параметры
            typeof(ILGeneratorDemo).Module
        );

        // Получаем ILGenerator
        ILGenerator il = dynamicMethod.GetILGenerator();

        // Генерируем IL-код
        il.Emit(OpCodes.Ldarg_0);      // загрузить первый аргумент
        il.Emit(OpCodes.Ldarg_1);      // загрузить второй аргумент
        il.Emit(OpCodes.Add);          // сложить
        il.Emit(OpCodes.Ret);          // вернуть результат

        // Создаём делегат
        return (Func<int, int, int>)dynamicMethod.CreateDelegate(typeof(Func<int, int, int>));
    }

    // Факториал ИТЕРАТИВНО (циклом) — вариант из методички
    public static Func<int, int> CreateFactorialMethod()
    {
        DynamicMethod dynamicMethod = new DynamicMethod(
            "DynamicFactorial",
            typeof(int),
            new[] { typeof(int) },
            typeof(ILGeneratorDemo).Module
        );

        ILGenerator il = dynamicMethod.GetILGenerator();

        // Метки для переходов
        Label startLoop = il.DefineLabel();
        Label endLoop = il.DefineLabel();

        // Локальные переменные
        il.DeclareLocal(typeof(int)); // result
        il.DeclareLocal(typeof(int)); // i

        // result = 1;
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stloc_0);

        // i = 1;
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stloc_1);

        // startLoop:
        il.MarkLabel(startLoop);

        // if (i > arg0) goto endLoop;
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Bgt, endLoop);

        // result *= i;
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Mul);
        il.Emit(OpCodes.Stloc_0);

        // i++;
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc_1);

        // goto startLoop;
        il.Emit(OpCodes.Br, startLoop);

        // endLoop:
        il.MarkLabel(endLoop);

        // return result;
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ret);

        return (Func<int, int>)dynamicMethod.CreateDelegate(typeof(Func<int, int>));
    }

    // Определение знака числа — вариант из методички
    public static Func<int, string> CreateGradeMethod()
    {
        DynamicMethod dynamicMethod = new DynamicMethod(
            "DynamicGrade",
            typeof(string),
            new[] { typeof(int) },
            typeof(ILGeneratorDemo).Module
        );

        ILGenerator il = dynamicMethod.GetILGenerator();

        Label positive = il.DefineLabel();
        Label negative = il.DefineLabel();

        // if (value > 0) goto positive;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Bgt, positive);

        // if (value < 0) goto negative;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Blt, negative);

        // return "Zero";
        il.Emit(OpCodes.Ldstr, "Zero");
        il.Emit(OpCodes.Ret);

        // positive:
        il.MarkLabel(positive);
        il.Emit(OpCodes.Ldstr, "Positive");
        il.Emit(OpCodes.Ret);

        // negative:
        il.MarkLabel(negative);
        il.Emit(OpCodes.Ldstr, "Negative");
        il.Emit(OpCodes.Ret);

        return (Func<int, string>)dynamicMethod.CreateDelegate(typeof(Func<int, string>));
    }

    // ==================== ЗАДАНИЕ 2.1 ====================
    // Динамический метод, вычисляющий n! РЕКУРСИВНО
    //
    // Эквивалентный код на C#:
    //   int Factorial(int n)
    //   {
    //       if (n <= 1) return 1;
    //       return n * Factorial(n - 1);
    //   }
    public static Func<int, int> CreateRecursiveFactorialMethod()
    {
        DynamicMethod dynamicMethod = new DynamicMethod(
            "DynamicFactorialRecursive",
            typeof(int),
            new[] { typeof(int) },
            typeof(ILGeneratorDemo).Module
        );

        ILGenerator il = dynamicMethod.GetILGenerator();

        // Метка на ветку рекурсии
        Label recursiveCase = il.DefineLabel();

        // if (n > 1) goto recursiveCase;
        il.Emit(OpCodes.Ldarg_0);                 // стек: [n]
        il.Emit(OpCodes.Ldc_I4_1);                // стек: [n, 1]
        il.Emit(OpCodes.Bgt, recursiveCase);      // стек: []

        // База рекурсии: return 1;
        il.Emit(OpCodes.Ldc_I4_1);                // стек: [1]
        il.Emit(OpCodes.Ret);

        // recursiveCase:
        il.MarkLabel(recursiveCase);

        // return n * DynamicFactorialRecursive(n - 1);
        il.Emit(OpCodes.Ldarg_0);                 // стек: [n]
        il.Emit(OpCodes.Ldarg_0);                 // стек: [n, n]
        il.Emit(OpCodes.Ldc_I4_1);                // стек: [n, n, 1]
        il.Emit(OpCodes.Sub);                     // стек: [n, n-1]
        il.Emit(OpCodes.Call, dynamicMethod);     // рекурсивный вызов самого себя, стек: [n, (n-1)!]
        il.Emit(OpCodes.Mul);                     // стек: [n * (n-1)!]
        il.Emit(OpCodes.Ret);

        return (Func<int, int>)dynamicMethod.CreateDelegate(typeof(Func<int, int>));
    }

    // ==================== ЗАДАНИЕ 2.2 ====================
    // Динамический метод, находящий максимум из трёх чисел
    //
    // Эквивалентный код на C#:
    //   int Max3(int a, int b, int c)
    //   {
    //       int max = a;
    //       if (b > max) max = b;
    //       if (c > max) max = c;
    //       return max;
    //   }
    public static Func<int, int, int, int> CreateMaxOfThreeMethod()
    {
        DynamicMethod dynamicMethod = new DynamicMethod(
            "DynamicMaxOfThree",
            typeof(int),
            new[] { typeof(int), typeof(int), typeof(int) },
            typeof(ILGeneratorDemo).Module
        );

        ILGenerator il = dynamicMethod.GetILGenerator();

        // Локальная переменная 0 — текущий максимум
        il.DeclareLocal(typeof(int));

        Label skipB = il.DefineLabel();
        Label skipC = il.DefineLabel();

        // max = a;
        il.Emit(OpCodes.Ldarg_0);            // стек: [a]
        il.Emit(OpCodes.Stloc_0);            // max = a, стек: []

        // if (b <= max) goto skipB;
        il.Emit(OpCodes.Ldarg_1);            // стек: [b]
        il.Emit(OpCodes.Ldloc_0);            // стек: [b, max]
        il.Emit(OpCodes.Ble, skipB);         // стек: []

        // max = b;
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stloc_0);

        // skipB:
        il.MarkLabel(skipB);

        // if (c <= max) goto skipC;
        il.Emit(OpCodes.Ldarg_2);            // стек: [c]
        il.Emit(OpCodes.Ldloc_0);            // стек: [c, max]
        il.Emit(OpCodes.Ble, skipC);         // стек: []

        // max = c;
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Stloc_0);

        // skipC:
        il.MarkLabel(skipC);

        // return max;
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ret);

        return (Func<int, int, int, int>)dynamicMethod.CreateDelegate(typeof(Func<int, int, int, int>));
    }
}
