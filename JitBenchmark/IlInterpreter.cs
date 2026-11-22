using System.Reflection;
using System.Reflection.Emit;

namespace JBenchMark;

/// <summary>
/// Простейший интерпретатор IL-кода.
/// Выполняет байты IL-метода командой за командой, программно моделируя стек вычислений.
/// Нужен, чтобы получить выполнение того же самого метода БЕЗ JIT-компиляции:
/// JIT переводит IL в машинный код один раз, а интерпретатор каждый раз заново
/// разбирает байты и выполняет их средствами обычного C#-кода.
/// </summary>
public static class IlInterpreter
{
    // Таблица кодов операций, собранная через рефлексию класса OpCodes
    private static readonly Dictionary<short, OpCode> OpCodeTable = BuildOpCodeTable();

    private static Dictionary<short, OpCode> BuildOpCodeTable()
    {
        var table = new Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetValue(null) is OpCode op)
                table[op.Value] = op;
        return table;
    }

    /// <summary>
    /// Выполняет метод, интерпретируя его IL-код.
    /// </summary>
    public static object? Execute(MethodInfo method, params object[] arguments)
    {
        var body = method.GetMethodBody()
                   ?? throw new InvalidOperationException("Тело метода недоступно");
        byte[] il = body.GetILAsByteArray()
                    ?? throw new InvalidOperationException("IL-код недоступен");

        // Стек вычислений и ячейки локальных переменных
        var stack = new Stack<object?>();
        var locals = new object?[body.LocalVariables.Count];

        int pos = 0;
        while (pos < il.Length)
        {
            // Читаем код операции (однобайтовый либо с префиксом 0xFE)
            short code = il[pos++];
            if (code == 0xFE) code = (short)(0xFE00 | il[pos++]);

            if (!OpCodeTable.TryGetValue(code, out OpCode op))
                throw new NotSupportedException($"Неизвестный код операции 0x{code:X2}");

            switch (op.Name)
            {
                // ---- ничего не делает (расставляется компилятором в Debug) ----
                case "nop":
                case "break":
                    break;

                // ---- загрузка аргументов ----
                case "ldarg.0": stack.Push(arguments[0]); break;
                case "ldarg.1": stack.Push(arguments[1]); break;
                case "ldarg.2": stack.Push(arguments[2]); break;
                case "ldarg.3": stack.Push(arguments[3]); break;
                case "ldarg.s": stack.Push(arguments[il[pos++]]); break;

                // ---- загрузка целых констант ----
                case "ldc.i4.m1": stack.Push(-1); break;
                case "ldc.i4.0": stack.Push(0); break;
                case "ldc.i4.1": stack.Push(1); break;
                case "ldc.i4.2": stack.Push(2); break;
                case "ldc.i4.3": stack.Push(3); break;
                case "ldc.i4.4": stack.Push(4); break;
                case "ldc.i4.5": stack.Push(5); break;
                case "ldc.i4.6": stack.Push(6); break;
                case "ldc.i4.7": stack.Push(7); break;
                case "ldc.i4.8": stack.Push(8); break;
                case "ldc.i4.s": stack.Push((int)(sbyte)il[pos++]); break;
                case "ldc.i4":
                    stack.Push(BitConverter.ToInt32(il, pos));
                    pos += 4;
                    break;

                // ---- чтение локальных переменных ----
                case "ldloc.0": stack.Push(locals[0]); break;
                case "ldloc.1": stack.Push(locals[1]); break;
                case "ldloc.2": stack.Push(locals[2]); break;
                case "ldloc.3": stack.Push(locals[3]); break;
                case "ldloc.s": stack.Push(locals[il[pos++]]); break;

                // ---- запись локальных переменных ----
                case "stloc.0": locals[0] = stack.Pop(); break;
                case "stloc.1": locals[1] = stack.Pop(); break;
                case "stloc.2": locals[2] = stack.Pop(); break;
                case "stloc.3": locals[3] = stack.Pop(); break;
                case "stloc.s": locals[il[pos++]] = stack.Pop(); break;

                // ---- работа со стеком ----
                case "dup": stack.Push(stack.Peek()); break;
                case "pop": stack.Pop(); break;

                // ---- арифметика ----
                case "add": { int b = Pop(stack), a = Pop(stack); stack.Push(a + b); break; }
                case "sub": { int b = Pop(stack), a = Pop(stack); stack.Push(a - b); break; }
                case "mul": { int b = Pop(stack), a = Pop(stack); stack.Push(a * b); break; }
                case "div": { int b = Pop(stack), a = Pop(stack); stack.Push(a / b); break; }
                case "rem": { int b = Pop(stack), a = Pop(stack); stack.Push(a % b); break; }
                case "neg": stack.Push(-Pop(stack)); break;

                // ---- массивы ----
                case "ldlen": stack.Push(((Array)stack.Pop()!).Length); break;
                case "ldelem.i4":
                {
                    int index = Pop(stack);
                    var array = (int[])stack.Pop()!;
                    stack.Push(array[index]);
                    break;
                }
                case "conv.i4": stack.Push(Pop(stack)); break;

                // ---- сравнения (кладут на стек 0 или 1) ----
                case "ceq": { int b = Pop(stack), a = Pop(stack); stack.Push(a == b ? 1 : 0); break; }
                case "cgt": { int b = Pop(stack), a = Pop(stack); stack.Push(a > b ? 1 : 0); break; }
                case "clt": { int b = Pop(stack), a = Pop(stack); stack.Push(a < b ? 1 : 0); break; }

                // ---- безусловные переходы ----
                case "br":   pos = Target(il, ref pos, shortForm: false); break;
                case "br.s": pos = Target(il, ref pos, shortForm: true); break;

                // ---- условные переходы ----
                case "brtrue":
                case "brtrue.s":
                {
                    bool shortForm = op.Name.EndsWith(".s");
                    int target = Target(il, ref pos, shortForm);
                    if (Pop(stack) != 0) pos = target;
                    break;
                }
                case "brfalse":
                case "brfalse.s":
                {
                    bool shortForm = op.Name.EndsWith(".s");
                    int target = Target(il, ref pos, shortForm);
                    if (Pop(stack) == 0) pos = target;
                    break;
                }
                case "blt": case "blt.s":
                case "ble": case "ble.s":
                case "bgt": case "bgt.s":
                case "bge": case "bge.s":
                case "beq": case "beq.s":
                case "bne.un": case "bne.un.s":
                {
                    bool shortForm = op.Name.EndsWith(".s");
                    int b = Pop(stack), a = Pop(stack);
                    int target = Target(il, ref pos, shortForm);
                    bool jump = op.Name.Split('.')[0] switch
                    {
                        "blt" => a < b,
                        "ble" => a <= b,
                        "bgt" => a > b,
                        "bge" => a >= b,
                        "beq" => a == b,
                        "bne" => a != b,
                        _ => false
                    };
                    if (jump) pos = target;
                    break;
                }

                // ---- возврат ----
                case "ret":
                    return stack.Count > 0 ? stack.Pop() : null;

                default:
                    throw new NotSupportedException(
                        $"Инструкция {op.Name} не поддерживается интерпретатором");
            }
        }

        return null;
    }

    // Снять со стека целое число
    private static int Pop(Stack<object?> stack) => Convert.ToInt32(stack.Pop());

    // Вычислить абсолютный адрес перехода: смещение считается от следующей инструкции
    private static int Target(byte[] il, ref int pos, bool shortForm)
    {
        if (shortForm)
        {
            sbyte delta = (sbyte)il[pos++];
            return pos + delta;
        }
        int wide = BitConverter.ToInt32(il, pos);
        pos += 4;
        return pos + wide;
    }
}
