using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SSP_Lab5;

public class SlauTask
{
    private const int N = 1000;

    public static void Run()
    {
        Console.WriteLine("=== СЛАУ методом Гаусса ===\n");

        var (A1, b1) = GenerateSystem(N);
        var (A2, b2) = GenerateSystem(N);

        var sw = Stopwatch.StartNew();
        double[] xSeq = GaussSequential(A1, b1);
        sw.Stop();
        Console.WriteLine($"Последовательно:  {sw.ElapsedMilliseconds} мс");

        sw.Restart();
        double[] xPar = GaussParallel(A2, b2);
        sw.Stop();
        Console.WriteLine($"Параллельно:      {sw.ElapsedMilliseconds} мс");

        double error = 0;
        for (int i = 0; i < N; i++)
            error = Math.Max(error, Math.Abs(xSeq[i] - xPar[i]));
        Console.WriteLine($"Макс. погрешность: {error:E3}");
    }

    // Генерируем систему с диагональным преобладанием — гарантирует единственное решение
    private static (double[,], double[]) GenerateSystem(int n)
    {
        var rng = new Random(42);
        var A = new double[n, n];
        var b = new double[n];

        for (int i = 0; i < n; i++)
        {
            double rowSum = 0;
            for (int j = 0; j < n; j++)
            {
                if (i != j)
                {
                    A[i, j] = rng.NextDouble() * 2 - 1;
                    rowSum += Math.Abs(A[i, j]);
                }
            }
            A[i, i] = rowSum + rng.NextDouble() + 1;
            b[i] = rng.NextDouble() * 10;
        }
        return (A, b);
    }

    private static double[] GaussSequential(double[,] A, double[] b)
    {
        int n = b.Length;
        double[,] M = BuildAugmented(A, b, n);

        for (int k = 0; k < n; k++)
        {
            Pivot(M, k, n);

            // Последовательное исключение строк ниже k
            for (int i = k + 1; i < n; i++)
                EliminateRow(M, i, k, n);
        }

        return BackSubstitution(M, n);
    }

    private static double[] GaussParallel(double[,] A, double[] b)
    {
        int n = b.Length;
        double[,] M = BuildAugmented(A, b, n);

        for (int k = 0; k < n; k++)
        {
            // Выбор ведущего элемента — последовательно (зависит от текущего k)
            Pivot(M, k, n);

            // Исключение строк — каждая строка независима, параллелим
            Parallel.For(k + 1, n, i => EliminateRow(M, i, k, n));
        }

        return BackSubstitution(M, n);
    }

    private static double[,] BuildAugmented(double[,] A, double[] b, int n)
    {
        var M = new double[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                M[i, j] = A[i, j];
            M[i, n] = b[i];
        }
        return M;
    }

    // Частичный выбор ведущего элемента (swap строки с максимальным |M[i,k]|)
    private static void Pivot(double[,] M, int k, int n)
    {
        int maxRow = k;
        for (int i = k + 1; i < n; i++)
            if (Math.Abs(M[i, k]) > Math.Abs(M[maxRow, k]))
                maxRow = i;

        if (maxRow != k)
            for (int j = 0; j <= n; j++)
                (M[k, j], M[maxRow, j]) = (M[maxRow, j], M[k, j]);
    }

    // Вычитаем из строки i строку k, умноженную на коэффициент
    private static void EliminateRow(double[,] M, int i, int k, int n)
    {
        double factor = M[i, k] / M[k, k];
        for (int j = k; j <= n; j++)
            M[i, j] -= factor * M[k, j];
    }

    // Обратный ход — считаем x снизу вверх
    private static double[] BackSubstitution(double[,] M, int n)
    {
        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            x[i] = M[i, n];
            for (int j = i + 1; j < n; j++)
                x[i] -= M[i, j] * x[j];
            x[i] /= M[i, i];
        }
        return x;
    }
}
