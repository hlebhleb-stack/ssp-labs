using System.Reflection;
using System.Reflection.Emit;

namespace IlAnalysis;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== Лабораторная работа 1: Анализ IL-кода ===\n");

        // --- Примеры из методических указаний ---
        AnalyzeIL("Метод SimpleMethod", SimpleMethod);
        AnalyzeIL("Метод MethodWithLoop", MethodWithLoop);
        AnalyzeIL("Метод MethodWithCondition", MethodWithCondition);

        // --- ЗАДАНИЕ 1: собственный метод (произведение двух чисел) ---
        Console.WriteLine("\n############ ЗАДАНИЕ 1 ############");
        Console.WriteLine($"Проверка работы: MultiplyMethod(7, 6) = {MultiplyMethod(7, 6)}");
        Console.WriteLine($"Проверка работы: MultiplyMethod(-3, 12) = {MultiplyMethod(-3, 12)}");
        AnalyzeIL("Метод MultiplyMethod", MultiplyMethod);

        // Для сравнения: то же произведение, но с накоплением в цикле
        Console.WriteLine($"Проверка работы: MultiplyByLoop(7, 6) = {MultiplyByLoop(7, 6)}");
        AnalyzeIL("Метод MultiplyByLoop", MultiplyByLoop);
    }

    // ---------- Анализируемые методы ----------

    // Простой метод
    static int SimpleMethod(int a, int b) => a + b;

    // Метод с циклом
    static int MethodWithLoop(int n)
    {
        int sum = 0;
        for (int i = 1; i <= n; i++)
            sum += i;
        return sum;
    }

    // Метод с условием
    static string MethodWithCondition(int value)
    {
        if (value > 0)
            return "Positive";
        else if (value < 0)
            return "Negative";
        else
            return "Zero";
    }

    // ЗАДАНИЕ 1: произведение двух целых чисел
    static int MultiplyMethod(int a, int b) => a * b;

    // ЗАДАНИЕ 1: то же произведение через сложение в цикле (для сравнения IL)
    static int MultiplyByLoop(int a, int b)
    {
        int result = 0;
        int count = Math.Abs(b);
        for (int i = 0; i < count; i++)
            result += a;
        return b < 0 ? -result : result;
    }

    // ---------- Вспомогательный метод для анализа IL ----------

    static void AnalyzeIL(string methodName, Delegate method)
    {
        Console.WriteLine($"\n┌─────────────────────────────────────────────┐");
        Console.WriteLine($"│ Анализ: {methodName,-36}│");
        Console.WriteLine($"└─────────────────────────────────────────────┘");

        var methodInfo = method.Method;
        var methodBody = methodInfo.GetMethodBody();

        if (methodBody == null)
        {
            Console.WriteLine("Тело метода не доступно (Release режим?)");
            return;
        }

        byte[]? ilBytes = methodBody.GetILAsByteArray();
        if (ilBytes == null || ilBytes.Length == 0)
        {
            Console.WriteLine("IL-код не получен");
            return;
        }

        Console.WriteLine($"Сигнатура: {Signature(methodInfo)}");
        Console.WriteLine($"Размер IL: {ilBytes.Length} байт");
        Console.WriteLine($"Локальные переменные: {methodBody.LocalVariables.Count}");
        foreach (var local in methodBody.LocalVariables)
            Console.WriteLine($"   [{local.LocalIndex}] {local.LocalType?.Name}");
        Console.WriteLine($"Максимальный размер стека: {methodBody.MaxStackSize}");

        Console.WriteLine("\nIL-инструкции (HEX):");
        for (int i = 0; i < ilBytes.Length; i++)
        {
            Console.Write($"{ilBytes[i]:X2} ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
        Console.WriteLine();

        Console.WriteLine("\nIL-инструкции (расшифровка):");
        PrintDisassembly(ilBytes, methodInfo);
        Console.WriteLine();
    }

    static string Signature(MethodInfo mi)
    {
        var ps = string.Join(", ", mi.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        return $"{mi.ReturnType.Name} {mi.Name}({ps})";
    }

    // ---------- Мини-дизассемблер IL ----------

    // Таблица всех кодов операций, собранная через рефлексию класса OpCodes
    static readonly Dictionary<short, OpCode> OpCodeTable = BuildOpCodeTable();

    static Dictionary<short, OpCode> BuildOpCodeTable()
    {
        var table = new Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode op)
                table[op.Value] = op;
        }
        return table;
    }

    static void PrintDisassembly(byte[] il, MethodInfo methodInfo)
    {
        var module = methodInfo.Module;
        int pos = 0;

        while (pos < il.Length)
        {
            int offset = pos;

            short code = il[pos++];
            if (code == 0xFE && pos < il.Length)
                code = (short)(0xFE00 | il[pos++]);

            if (!OpCodeTable.TryGetValue(code, out OpCode op))
            {
                Console.WriteLine($"  IL_{offset:X4}:  <неизвестный код 0x{code:X2}>");
                continue;
            }

            string operand = ReadOperand(il, ref pos, op, module);
            Console.WriteLine($"  IL_{offset:X4}:  {op.Name,-12} {operand}");
        }
    }

    static string ReadOperand(byte[] il, ref int pos, OpCode op, Module module)
    {
        switch (op.OperandType)
        {
            case OperandType.InlineNone:
                return "";

            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                return il[pos++].ToString();

            case OperandType.ShortInlineBrTarget:
            {
                sbyte delta = (sbyte)il[pos++];
                return $"IL_{pos + delta:X4}";
            }

            case OperandType.InlineVar:
            {
                short v = BitConverter.ToInt16(il, pos);
                pos += 2;
                return v.ToString();
            }

            case OperandType.InlineI:
            {
                int v = BitConverter.ToInt32(il, pos);
                pos += 4;
                return v.ToString();
            }

            case OperandType.InlineBrTarget:
            {
                int delta = BitConverter.ToInt32(il, pos);
                pos += 4;
                return $"IL_{pos + delta:X4}";
            }

            case OperandType.ShortInlineR:
            {
                float v = BitConverter.ToSingle(il, pos);
                pos += 4;
                return v.ToString();
            }

            case OperandType.InlineI8:
            {
                long v = BitConverter.ToInt64(il, pos);
                pos += 8;
                return v.ToString();
            }

            case OperandType.InlineR:
            {
                double v = BitConverter.ToDouble(il, pos);
                pos += 8;
                return v.ToString();
            }

            case OperandType.InlineString:
            {
                int token = BitConverter.ToInt32(il, pos);
                pos += 4;
                try { return $"\"{module.ResolveString(token)}\""; }
                catch { return $"(строка 0x{token:X8})"; }
            }

            case OperandType.InlineMethod:
            case OperandType.InlineField:
            case OperandType.InlineType:
            case OperandType.InlineTok:
            {
                int token = BitConverter.ToInt32(il, pos);
                pos += 4;
                try { return module.ResolveMember(token)?.Name ?? $"0x{token:X8}"; }
                catch { return $"0x{token:X8}"; }
            }

            case OperandType.InlineSig:
            {
                pos += 4;
                return "(сигнатура)";
            }

            case OperandType.InlineSwitch:
            {
                int count = BitConverter.ToInt32(il, pos);
                pos += 4 + 4 * count;
                return $"(switch, {count} веток)";
            }

            default:
                return "";
        }
    }
}
