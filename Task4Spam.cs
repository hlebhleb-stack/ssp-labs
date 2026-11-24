using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

internal static class Task4Spam
{
    public static void Run(MLContext ml)
    {
        // Каждый столбец CSV считывается по индексу из атрибутов EmailData.
        var data = ml.Data.LoadFromTextFile<EmailData>(
            Path.Combine(AppContext.BaseDirectory, "spam.csv"), hasHeader: true, separatorChar: ',');

        // Тема и тело превращаются в два числовых вектора.
        // К ним добавляются два признака 0/1. IsSpam остаётся только целевой меткой.
        var textFeatures = ml.Transforms.Text.FeaturizeText("SubjectFeatures", nameof(EmailData.Subject))
            .Append(ml.Transforms.Text.FeaturizeText("MessageFeatures", nameof(EmailData.Message)));

        var trainer = new SdcaLogisticRegressionBinaryTrainer.Options
        {
            LabelColumnName = nameof(EmailData.IsSpam),
            MaximumNumberOfIterations = 100,
            NumberOfThreads = 1
        };

        var pipeline = textFeatures
            .Append(ml.Transforms.Concatenate("Features",
                "SubjectFeatures", "MessageFeatures", nameof(EmailData.SenderKnown), nameof(EmailData.HasAttachments)))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(trainer));

        // ApplyWordEmbedding сопоставляет токены с векторами из учебного русского словаря.
        // Сохраняем базовые признаки, чтобы проверить вклад добавленных векторов отдельно.
        var vectorPath = Path.Combine(AppContext.BaseDirectory, "ru-word-vectors.txt");
        var embeddingPipeline = textFeatures
            // Нулевой маркер обеспечивает конечный результат даже для текста без слов из словаря.
            .Append(ml.Transforms.CustomMapping<EmailData, EmbeddingText>((email, output) =>
            {
                output.EmbeddingSubject = "нейтральныймаркер " + email.Subject;
                output.EmbeddingMessage = "нейтральныймаркер " + email.Message;
            }, contractName: null))
            .Append(ml.Transforms.Text.NormalizeText("NormalizedSubject", nameof(EmbeddingText.EmbeddingSubject)))
            .Append(ml.Transforms.Text.NormalizeText("NormalizedMessage", nameof(EmbeddingText.EmbeddingMessage)))
            .Append(ml.Transforms.Text.TokenizeIntoWords("SubjectTokens", "NormalizedSubject"))
            .Append(ml.Transforms.Text.TokenizeIntoWords("MessageTokens", "NormalizedMessage"))
            .Append(ml.Transforms.Text.ApplyWordEmbedding(outputColumnName: "SubjectEmbedding", inputColumnName: "SubjectTokens",
                customModelFile: vectorPath))
            .Append(ml.Transforms.Text.ApplyWordEmbedding(outputColumnName: "MessageEmbedding", inputColumnName: "MessageTokens",
                customModelFile: vectorPath))
            .Append(ml.Transforms.Concatenate("Features",
                "SubjectFeatures", "MessageFeatures", "SubjectEmbedding", "MessageEmbedding",
                nameof(EmailData.SenderKnown), nameof(EmailData.HasAttachments)))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(trainer));

        // Сравниваем модели на одних и тех же тестовых письмах, не передавая их в Fit.
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2);
        Evaluate(ml, pipeline, split, "Базовая модель");
        Evaluate(ml, embeddingPipeline, split, "С ApplyWordEmbedding");

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

    private static void Evaluate(MLContext ml, IEstimator<ITransformer> pipeline,
        DataOperationsCatalog.TrainTestData split, string name)
    {
        var model = pipeline.Fit(split.TrainSet);
        var predictions = model.Transform(split.TestSet);
        var metrics = ml.BinaryClassification.Evaluate(predictions, labelColumnName: nameof(EmailData.IsSpam));

        // Диагональ матрицы ошибок содержит правильные ответы по обоим классам.
        var counts = metrics.ConfusionMatrix.Counts;
        var correct = counts[0][0] + counts[1][1];
        Console.WriteLine($"{name}: {correct}/{counts.Sum(row => row.Sum())} ({metrics.Accuracy:P2})");
    }

    private sealed class EmbeddingText
    {
        public string EmbeddingSubject { get; set; } = string.Empty;
        public string EmbeddingMessage { get; set; } = string.Empty;
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
