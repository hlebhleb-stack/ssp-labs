using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

internal static class Task2Regression
{
    public static void Run(MLContext ml)
    {
        // Загружаем часы и фактические баллы из CSV, скопированного при сборке.
        var data = ml.Data.LoadFromTextFile<StudentData>(
            Path.Combine(AppContext.BaseDirectory, "data.csv"), hasHeader: true, separatorChar: ',');
        // Тестовые строки не участвуют в обучении регрессии.
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2);

        // Сохраняем фактический балл в Label: выход регрессии тоже называется Score.
        var pipeline = ml.Transforms.CopyColumns("Label", nameof(StudentData.Score))
            .Append(ml.Transforms.Concatenate("Features", nameof(StudentData.StudyHours)))
            .Append(ml.Regression.Trainers.Sdca(new SdcaRegressionTrainer.Options
            {
                MaximumNumberOfIterations = 100,
                NumberOfThreads = 1
            }));

        // Fit строит модель, Transform добавляет прогноз Score к тестовым строкам.
        var model = pipeline.Fit(split.TrainSet);
        var predictions = model.Transform(split.TestSet);
        var testRows = ml.Data.CreateEnumerable<StudentEvaluation>(predictions, reuseRowObject: false).ToArray();

        Console.WriteLine($"Строк в тестовой выборке: {testRows.Length}");
        if (testRows.Length > 0)
        {
            var metrics = ml.Regression.Evaluate(predictions);
            Console.WriteLine(testRows.Select(row => row.Label).Distinct().Count() > 1
                ? $"Коэффициент детерминации (R²): {metrics.RSquared:F2}"
                : "R² не определён: в тестовой выборке нет разброса фактических баллов.");
            Console.WriteLine($"Средняя абсолютная ошибка (MAE): {metrics.MeanAbsoluteError:F2}");
            Console.WriteLine($"Корень из средней квадратичной ошибки (RMSE): {metrics.RootMeanSquaredError:F2}");
        }

        // Движок строит прогноз для одной новой записи (6 часов).
        var predictor = ml.Model.CreatePredictionEngine<StudentData, StudentPrediction>(model);
        var student = new StudentData { StudyHours = 6 };
        Console.WriteLine($"Прогноз для {student.StudyHours} часов занятий: {predictor.Predict(student).Score:F2}");
    }

    private sealed class StudentData
    {
        [LoadColumn(0)] public float StudyHours { get; set; }
        [LoadColumn(1)] public float Score { get; set; }
    }

    private sealed class StudentPrediction
    {
        public float Score { get; set; }
    }

    private sealed class StudentEvaluation
    {
        public float Label { get; set; }
    }
}
