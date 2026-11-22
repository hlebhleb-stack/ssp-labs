using Microsoft.ML;
using Microsoft.ML.Data;

internal static class Task4Spam
{
    public static void Run(MLContext ml)
    {
        var data = ml.Data.LoadFromTextFile<EmailData>(
            Path.Combine(AppContext.BaseDirectory, "spam.csv"), hasHeader: true, separatorChar: ',');

        // Тема и тело превращаются в два числовых вектора.
        // К ним добавляются два признака 0/1. IsSpam остаётся только целевой меткой.
        var pipeline = ml.Transforms.Text.FeaturizeText("SubjectFeatures", nameof(EmailData.Subject))
            .Append(ml.Transforms.Text.FeaturizeText("MessageFeatures", nameof(EmailData.Message)))
            .Append(ml.Transforms.Concatenate("Features",
                "SubjectFeatures", "MessageFeatures", nameof(EmailData.SenderKnown), nameof(EmailData.HasAttachments)))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: nameof(EmailData.IsSpam)));

        // Для оценки обучаемся отдельно: тестовые письма не участвуют в Fit.
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2);
        var evaluationModel = pipeline.Fit(split.TrainSet);
        var predictions = evaluationModel.Transform(split.TestSet);
        var metrics = ml.BinaryClassification.Evaluate(predictions, labelColumnName: nameof(EmailData.IsSpam));
        var matrix = metrics.ConfusionMatrix;
        var correct = matrix.Counts[0][0] + matrix.Counts[1][1];
        Console.WriteLine($"Точность на тестовой выборке: {correct}/{matrix.Counts.Sum(row => row.Sum())} ({metrics.Accuracy:P2})");

        // Итоговую модель для демонстрации обучаем на всех размеченных письмах.
        var model = pipeline.Fit(data);
        var predictor = ml.Model.CreatePredictionEngine<EmailData, BinaryPrediction>(model);
        var email = new EmailData
        {
            Subject = "Срочно заберите денежный приз",
            Message = "Вы выиграли подарок. Перейдите по ссылке и подтвердите данные карты",
            SenderKnown = 0,
            HasAttachments = 0
        };
        var prediction = predictor.Predict(email);

        Console.WriteLine($"Тема: {email.Subject}");
        Console.WriteLine($"Текст: {email.Message}");
        Console.WriteLine($"Известный отправитель: {email.SenderKnown}. Вложения: {email.HasAttachments}.");
        Console.WriteLine($"Предсказание: {(prediction.Prediction ? "Спам" : "Не спам")}");
        Console.WriteLine($"Вероятность спама: {prediction.Probability:P2}");
    }

    private sealed class EmailData
    {
        [LoadColumn(0)] public string Subject { get; set; } = string.Empty;
        [LoadColumn(1)] public string Message { get; set; } = string.Empty;
        [LoadColumn(2)] public float SenderKnown { get; set; }
        [LoadColumn(3)] public float HasAttachments { get; set; }
        [LoadColumn(4)] public bool IsSpam { get; set; }
    }
}
